using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using PetProductivity.Server.Services;
using Xunit;

namespace PetProductivity.Tests;

public class TokenServiceTests
{
    private static TokenService Build()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
            ["Jwt:Issuer"] = "petproductivity-test"
        }).Build();
        return new TokenService(config);
    }

    [Fact]
    public void CreateToken_LlevaElUserIdEnSub_YElIssuer()
    {
        var svc = Build();
        var uid = Guid.NewGuid();

        var token = svc.CreateToken(uid, "Tester");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(uid.ToString(), jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("petproductivity-test", jwt.Issuer);
        Assert.True(jwt.ValidTo > DateTime.UtcNow); // no nace expirado
    }
}
