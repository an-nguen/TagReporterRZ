using Microsoft.EntityFrameworkCore;
using Moq;
using TagReporter.Datasource;
using TagReporter.Domains;
using TagReporter.Services;
using Xunit;

namespace TagReporter.Tests;

// https://www.thecodebuzz.com/unit-testing-mocking-dbcontext-mongo-db-asp-net-core/
public class NonQueryTest
{
    [Fact]
    public async void CreateAccount()
    {
        var mockSet = new Mock<DbSet<WstAccount>>();
        var mockContext = new Mock<TagReporterContext>();
        mockContext.Setup(m => m.WstAccounts).Returns(mockSet.Object);

        var service = new AccountService(mockContext.Object);
        await service.CreateAsync(new WstAccount("testemail", "pwd123"));
        
        mockSet.Verify(m => m.Add(It.IsAny<WstAccount>()), Times.Once);
        mockContext.Verify(m => m.SaveChanges(), Times.Once());
        Assert.True(true);
    }
}