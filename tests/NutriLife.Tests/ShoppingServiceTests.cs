using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ShoppingServiceTests
{
    [TestMethod]
    public void Test_Add_SavesCorrectData()
    {
        var service = new ShoppingService();
        var userId = Guid.NewGuid();

        var result = service.Add(userId, "Mleko", 2, "l", null);

        Assert.AreNotEqual(Guid.Empty, result.Id);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual("Mleko", result.Name);
        Assert.AreEqual(2m, result.Quantity);
        Assert.AreEqual("l", result.Unit);
        Assert.IsFalse(result.IsPurchased);
    }

    [TestMethod]
    public void Test_GetAll_PurchasedItemsAtEnd()
    {
        var service = new ShoppingService();
        var userId = Guid.NewGuid();

        var a = service.Add(userId, "Avokado", 1, null, null);
        service.Add(userId, "Banane", 1, null, null);
        service.SetPurchased(userId, a.Id, true);

        var result = service.GetAll(userId);

        Assert.AreEqual(2, result.Length);
        Assert.AreEqual("Banane", result[0].Name);
        Assert.AreEqual("Avokado", result[1].Name);
    }

    [TestMethod]
    public void Test_GetAll_SeparatesUsers()
    {
        var service = new ShoppingService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        service.Add(user1, "Jajca", 10, "kos", null);
        service.Add(user2, "Kruh", 1, "kos", null);

        var result = service.GetAll(user1);

        Assert.AreEqual(1, result.Length);
        Assert.AreEqual("Jajca", result[0].Name);
    }

    [TestMethod]
    public void Test_SetPurchased_MarksItem()
    {
        var service = new ShoppingService();
        var userId = Guid.NewGuid();
        var item = service.Add(userId, "Sir", 200, "g", null);

        var result = service.SetPurchased(userId, item.Id, true);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsPurchased);
    }

    [TestMethod]
    public void Test_Update_ChangesData()
    {
        var service = new ShoppingService();
        var userId = Guid.NewGuid();
        var item = service.Add(userId, "Mleko", 1, "l", null);

        var result = service.Update(userId, item.Id, "Ovseno mleko", 3, "l");

        Assert.IsNotNull(result);
        Assert.AreEqual("Ovseno mleko", result.Name);
        Assert.AreEqual(3m, result.Quantity);
    }

    [TestMethod]
    public void Test_Delete_OtherUser_ReturnsFalse()
    {
        var service = new ShoppingService();
        var item = service.Add(Guid.NewGuid(), "Sol", 1, "kg", null);

        Assert.IsFalse(service.Delete(Guid.NewGuid(), item.Id));
    }

    [TestMethod]
    public void Test_Delete_RemovesItem()
    {
        var service = new ShoppingService();
        var userId = Guid.NewGuid();
        var item = service.Add(userId, "Sol", 1, "kg", null);

        Assert.IsTrue(service.Delete(userId, item.Id));
        Assert.AreEqual(0, service.GetAll(userId).Length);
    }
}