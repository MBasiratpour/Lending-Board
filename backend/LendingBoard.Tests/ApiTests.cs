using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LendingBoard.Tests;

/// <summary>
/// Integration tests: they start the real API with a real (temporary) SQLite
/// database and send HTTP requests to it, like the frontend does.
/// Each test gets its own fresh database with the demo data.
/// </summary>
public class ApiTests : IDisposable
{
    private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"lendingboard-test-{Guid.NewGuid()}.db");
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:LendingDb"] = $"Data Source={_dbFile};Pooling=False"
                })));
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        File.Delete(_dbFile);
    }

    private async Task<JsonElement[]> GetArray(string url) =>
        (await _client.GetFromJsonAsync<JsonElement[]>(url))!;

    private async Task<int> MemberId(string name) =>
        (await GetArray("/api/members")).Single(m => m.GetProperty("name").GetString() == name)
            .GetProperty("id").GetInt32();

    private async Task<JsonElement> Item(string name) =>
        (await GetArray("/api/items")).Single(i => i.GetProperty("name").GetString() == name);

    [Fact]
    public async Task Board_shows_the_demo_items_with_their_status()
    {
        var items = await GetArray("/api/items");

        Assert.Equal(4, items.Length);
        var drill = await Item("Cordless drill");
        Assert.Equal("Approved", drill.GetProperty("status").GetString());
        Assert.True(drill.GetProperty("isOverdue").GetBoolean());
    }

    [Fact]
    public async Task Filter_only_available_hides_items_on_loan()
    {
        var items = await GetArray("/api/items?availableOnly=true");

        Assert.Equal(3, items.Length);
        Assert.DoesNotContain(items, i => i.GetProperty("name").GetString() == "Cordless drill");
    }

    [Fact]
    public async Task Search_and_category_filter_the_board()
    {
        var tools = await GetArray("/api/items?category=Tools");
        var ladder = await GetArray("/api/items?search=ladd");

        Assert.Equal(2, tools.Length);
        Assert.Equal("Ladder", Assert.Single(ladder).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Borrowing_your_own_item_is_answered_with_400()
    {
        var ben = await MemberId("Ben");
        var ladder = await Item("Ladder"); // Ben owns the ladder

        var response = await _client.PostAsJsonAsync(
            $"/api/items/{ladder.GetProperty("id").GetInt32()}/loans", new { memberId = ben });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("You cannot borrow your own item.", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Request_and_approve_with_a_return_date()
    {
        var anna = await MemberId("Anna");
        var ben = await MemberId("Ben");
        var ladderId = (await Item("Ladder")).GetProperty("id").GetInt32();

        // Anna asks to borrow Ben's ladder ...
        var request = await _client.PostAsJsonAsync($"/api/items/{ladderId}/loans", new { memberId = anna });
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var loanId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        // ... and Ben approves it until a date in 5 days.
        var dueAt = DateTime.UtcNow.AddDays(5);
        var approve = await _client.PostAsJsonAsync($"/api/loans/{loanId}/approve", new { memberId = ben, dueAt });
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var ladder = await Item("Ladder");
        Assert.Equal("Approved", ladder.GetProperty("status").GetString());
        Assert.False(ladder.GetProperty("isOverdue").GetBoolean());
    }
}
