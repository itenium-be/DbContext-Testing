DbContext Testing
=================

Examples of different approaches for testing Entity Framework Core DbContext.

## Running the Tests

```bash
dotnet test
```

## Testing Approaches

What are your options?


Other Differences
-----------------

- Concurrency/Optimistic Locking (`[Timestamp]`/RowVersion, `DbUpdateConcurrencyException`)
- Navigation Loading (Include vs Explicit vs Lazy - in-memory auto-populates from tracked entities)
- Bulk Operations (`ExecuteUpdateAsync`/`ExecuteDeleteAsync` bypass change tracker)
