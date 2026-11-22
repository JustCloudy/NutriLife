using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class AuthLoginLogoutTests
{
    [TestMethod]
    public void Login_ValidCredentials_ReturnsTokenAndUser()
    {
        var service = new AuthService();
        service.Register("david123", "david@example.com", "geslo123");

        var result = service.Login("david@example.com", "geslo123");

        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Value.Token));
        Assert.AreEqual("david123", result.Value.User.Username);
    }

    [TestMethod]
    public void Login_WrongPassword_ReturnsNull()
    {
        var service = new AuthService();
        service.Register("david123", "david@example.com", "geslo123");

        var result = service.Login("david@example.com", "napacnoGeslo");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Logout_ActiveToken_InvalidatesSession()
    {
        var service = new AuthService();
        service.Register("david123", "david@example.com", "geslo123");
        var login = service.Login("david@example.com", "geslo123")!.Value;

        Assert.IsNotNull(service.GetUserByToken(login.Token));

        var loggedOut = service.Logout(login.Token);

        Assert.IsTrue(loggedOut);
        Assert.IsNull(service.GetUserByToken(login.Token));
    }
}