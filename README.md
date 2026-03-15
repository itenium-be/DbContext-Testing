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

- Add a Foreign Key (InMemoryDatabase vs SQLite/SQLServer)
- How Includes work InMemory objects vs other
- Difference in null handling
- Constraints: `[Range(0, 1000)]`
- Transaction support
- Decimal precision storage
