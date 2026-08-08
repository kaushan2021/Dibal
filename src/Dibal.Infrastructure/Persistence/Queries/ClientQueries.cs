using Dibal.Domain.Entities;
using Dibal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dibal.Infrastructure.Persistence.Queries;

public enum ClientSortBy
{
    BusinessName,
    ClientName,
    Postcode,
    CustomerType,
    CreatedAt,
}

public record ClientListQuery(
    string? Search = null,
    CustomerType? CustomerType = null,
    ClientSortBy SortBy = ClientSortBy.BusinessName,
    bool Descending = false,
    int Page = 1,
    int PageSize = 20);

public record ClientListResult(IReadOnlyList<Client> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// CLAUDE.md invariant 5: search is always server-side. Offset pagination is
/// acceptable here (unlike stock/deliveries) — clients realistically never
/// exceed a few thousand rows, per docs/01-architecture.md's keyset-pagination
/// call-out being scoped to stock units and deliveries specifically.
/// </summary>
public static class ClientQueries
{
    public static async Task<ClientListResult> GetPagedAsync(
        DibalDbContext db, ClientListQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<Client> clients = db.Clients;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            var normalisedPostcode = query.Search.Trim().Replace(" ", "").ToUpperInvariant();

            // ILIKE on business_name/client_name is accelerated by the
            // pg_trgm GIN indexes for wildcard patterns; postcode is matched
            // on the normalised column so "sw1a 1aa" and "SW1A1AA" both hit.
            clients = clients.Where(c =>
                EF.Functions.ILike(c.BusinessName, pattern)
                || (c.ClientName != null && EF.Functions.ILike(c.ClientName, pattern))
                || (c.PostcodeNormalised != null && c.PostcodeNormalised.StartsWith(normalisedPostcode)));
        }

        if (query.CustomerType is { } customerType)
        {
            clients = clients.Where(c => c.CustomerType == customerType);
        }

        clients = ApplySort(clients, query.SortBy, query.Descending);

        var totalCount = await clients.CountAsync(cancellationToken);
        var items = await clients
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ClientListResult(items, totalCount, page, pageSize);
    }

    private static IQueryable<Client> ApplySort(IQueryable<Client> clients, ClientSortBy sortBy, bool descending) =>
        (sortBy, descending) switch
        {
            (ClientSortBy.BusinessName, false) => clients.OrderBy(c => c.BusinessName),
            (ClientSortBy.BusinessName, true) => clients.OrderByDescending(c => c.BusinessName),
            (ClientSortBy.ClientName, false) => clients.OrderBy(c => c.ClientName),
            (ClientSortBy.ClientName, true) => clients.OrderByDescending(c => c.ClientName),
            (ClientSortBy.Postcode, false) => clients.OrderBy(c => c.PostcodeNormalised),
            (ClientSortBy.Postcode, true) => clients.OrderByDescending(c => c.PostcodeNormalised),
            (ClientSortBy.CustomerType, false) => clients.OrderBy(c => c.CustomerType),
            (ClientSortBy.CustomerType, true) => clients.OrderByDescending(c => c.CustomerType),
            (ClientSortBy.CreatedAt, false) => clients.OrderBy(c => c.CreatedAt),
            (ClientSortBy.CreatedAt, true) => clients.OrderByDescending(c => c.CreatedAt),
            _ => clients.OrderBy(c => c.BusinessName),
        };
}
