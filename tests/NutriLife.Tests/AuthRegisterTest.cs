using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class AuthRegisterTests
{
    [TestMethod]
    public void Register_ValidData_CreatesUserAndHashesPassword()
    {
        var service = new AuthService();

        var (created, error, user) = service.Register("david123", "David@Example.com", "geslo123");

        Assert.IsTrue(created);
        Assert.IsNull(error);
        Assert.IsNotNull(user);
        Assert.AreEqual("david123", user.Username);
        Assert.AreEqual("david@example.com", user.Email);
        Assert.AreNotEqual("geslo123", user.PasswordHash);
    }

    [TestMethod]
    public void Register_DuplicateEmailOrUsername_ReturnsConflictError()
    {
        var service = new AuthService();
        service.Register("david123", "david@example.com", "geslo123");

        var (created, error, user) = service.Register("noviUporabnik", "david@example.com", "geslo456");

        Assert.IsFalse(created);
        Assert.IsNotNull(error);
        Assert.IsNull(user);
    }

    [TestMethod]
    public void ValidateRegistration_InvalidEmail_ReturnsValidationMessage()
    {
        var request = new RegisterRequest("david123", "neveljaven-email", "geslo123");

        var error = AuthEndpoints.ValidateRegistration(request);

        Assert.IsNotNull(error);
    }
}