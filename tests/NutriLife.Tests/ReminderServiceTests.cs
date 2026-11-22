using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ReminderServiceTests
{
    [TestMethod]
    public void Test_Add_SavesCorrectData()
    {
        var service = new ReminderService();
        var userId = Guid.NewGuid();

        var result = service.Add(userId, ReminderType.Water, new TimeOnly(9, 0), "Pij vodo");

        Assert.AreNotEqual(Guid.Empty, result.Id);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(ReminderType.Water, result.Type);
        Assert.AreEqual(new TimeOnly(9, 0), result.Time);
        Assert.AreEqual("Pij vodo", result.Message);
        Assert.IsTrue(result.IsActive);
    }

    [TestMethod]
    public void Test_GetAll_SortedByTime()
    {
        var service = new ReminderService();
        var userId = Guid.NewGuid();

        service.Add(userId, ReminderType.Meal, new TimeOnly(18, 0), null);
        service.Add(userId, ReminderType.Water, new TimeOnly(8, 0), null);

        var result = service.GetAll(userId);

        Assert.AreEqual(2, result.Length);
        Assert.AreEqual(new TimeOnly(8, 0), result[0].Time);
        Assert.AreEqual(new TimeOnly(18, 0), result[1].Time);
    }

    [TestMethod]
    public void Test_GetAll_SeparatesUsers()
    {
        var service = new ReminderService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        service.Add(user1, ReminderType.Water, new TimeOnly(9, 0), null);
        service.Add(user2, ReminderType.Meal, new TimeOnly(12, 0), null);

        var result = service.GetAll(user1);

        Assert.AreEqual(1, result.Length);
        Assert.AreEqual(ReminderType.Water, result[0].Type);
    }

    [TestMethod]
    public void Test_Update_ChangesData()
    {
        var service = new ReminderService();
        var userId = Guid.NewGuid();
        var reminder = service.Add(userId, ReminderType.Water, new TimeOnly(9, 0), null);

        var updated = service.Update(
            userId, reminder.Id, ReminderType.Activity, new TimeOnly(17, 30), "Sprehod", false);

        Assert.IsNotNull(updated);
        Assert.AreEqual(ReminderType.Activity, updated.Type);
        Assert.AreEqual(new TimeOnly(17, 30), updated.Time);
        Assert.IsFalse(updated.IsActive);
    }

    [TestMethod]
    public void Test_Update_OtherUser_ReturnsNull()
    {
        var service = new ReminderService();
        var reminder = service.Add(Guid.NewGuid(), ReminderType.Water, new TimeOnly(9, 0), null);

        var result = service.Update(
            Guid.NewGuid(), reminder.Id, ReminderType.Meal, new TimeOnly(10, 0), null, true);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Test_Delete_RemovesReminder()
    {
        var service = new ReminderService();
        var userId = Guid.NewGuid();
        var reminder = service.Add(userId, ReminderType.Water, new TimeOnly(9, 0), null);

        Assert.IsTrue(service.Delete(userId, reminder.Id));
        Assert.AreEqual(0, service.GetAll(userId).Length);
        Assert.IsFalse(service.Delete(userId, reminder.Id));
    }
}