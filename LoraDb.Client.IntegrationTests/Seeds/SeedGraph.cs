namespace LoraDb.Client.IntegrationTests.Seeds;

internal static class SeedGraph
{
    public static async Task CreateSocialGraph(LoraDbClient client)
    {
        using var result = await client.ExecuteAsync(
            """
            CREATE
              (alice:Person:User {name: 'Alice', age: 30, active: true, tags: ['admin', 'alpha'], label: 'user'}),
              (bob:Person:User {name: 'Bob', age: 40, active: false, tags: ['beta', 'team'], label: 'user'}),
              (carol:Person {name: 'Carol', age: 25, active: true, tags: ['gamma'], label: 'guest'}),
              (dave:Person {name: 'Dave', age: 45, active: true, tags: ['delta'], label: 'guest'}),
              (alice)-[:FOLLOWS {since: 2020, close: true}]->(bob),
              (bob)-[:KNOWS {since: 2018, strength: 1.5}]->(carol)
            """);
    }

    public static async Task TearDownGraph(LoraDbClient client)
    {
        using var result = await client.ExecuteAsync("MATCH (n) DETACH DELETE n");
    }

    public static async Task CreateProductGraph(LoraDbClient client)
    {
        using var result = await client.ExecuteAsync(
            """
            CREATE
              (hardware:Category {name: 'Hardware'}),
              (grocery:Category {name: 'Grocery'}),
              (keyboard:Product {name: 'Keyboard', price: 100, category: 'Hardware'}),
              (mouse:Product {name: 'Mouse', price: 50, category: 'Hardware'}),
              (coffee:Product {name: 'Coffee', price: 5, category: 'Grocery'}),
              (tea:Product {name: 'Tea', price: 7, category: 'Grocery'}),
              (keyboard)-[:IN_CATEGORY]->(hardware),
              (mouse)-[:IN_CATEGORY]->(hardware),
              (coffee)-[:IN_CATEGORY]->(grocery),
              (tea)-[:IN_CATEGORY]->(grocery)
            """);
    }
}
