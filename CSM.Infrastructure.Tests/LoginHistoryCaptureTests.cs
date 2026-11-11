using CSM.Application.Auth.Dtos;
using CSM.Application.Common.Security;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class LoginHistoryCaptureTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Known_user_attempt_is_recorded_with_bounded_metadata(bool success)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        Guid id;
        using (var db = new TestDb(options))
        {
            var user = new User { Email = "own@example.test", NormalizedEmail = "OWN@EXAMPLE.TEST", PasswordHash = "test" };
            db.Users.Add(user); await db.SaveChangesAsync(); id = user.Id;
            var settings = Options.Create(new JwtSettings { Issuer = "test", Audience = "test", SecretKey = "test-only-signing-key-with-at-least-32-characters" });
            var service = new AuthService(db, new TestHasher(), new TokenService(settings), settings);
            var request = new LoginRequest(user.Email, success ? "correct" : "incorrect", new string('x', 400));
            if (success) await service.LoginAsync(request, "127.0.0.1");
            else await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.AuthenticationException>(() => service.LoginAsync(request, "127.0.0.1"));
        }
        using var fresh = new TestDb(options);
        Assert.NotNull(fresh.Model.FindEntityType(typeof(LoginHistoryEntry)));
        var history = Assert.Single(await fresh.Set<LoginHistoryEntry>().Where(x => x.UserId == id).ToListAsync());
        Assert.Equal(success, history.Succeeded);
        Assert.InRange(history.DeviceName!.Length, 1, 256);
        Assert.DoesNotContain(success ? "correct" : "incorrect", history.FailureReason ?? "");
    }
    private sealed class TestHasher : IPasswordHasher
    {
        public string Hash(string password) => "test";
        public bool Verify(string password, string hash) => password == "correct";
    }
    private sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken ct = default) => base.SaveChangesAsync(true, ct);
    }
}
