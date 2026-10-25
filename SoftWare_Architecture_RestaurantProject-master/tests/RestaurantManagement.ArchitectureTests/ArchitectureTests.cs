using NetArchTest.Rules;
using Xunit;

namespace RestaurantManagement.ArchitectureTests;

public class LayerArchitectureTests
{
    private const string CommonNamespace = "RestaurantManagement.Common";
    private const string DataAccessNamespace = "RestaurantManagement.DataAccess";
    private const string BusinessNamespace = "RestaurantManagement.Business";
    private const string WebNamespace = "RestaurantManagement.Web";

    [Fact]
    public void Common_ShouldNotHaveDependencyOnOtherLayers()
    {
        var result = Types.InAssembly(typeof(RestaurantManagement.Common.Results.Result).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(DataAccessNamespace, BusinessNamespace, WebNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Common layer must not depend on DataAccess, Business, or Web.");
    }

    [Fact]
    public void Business_ShouldNotHaveDependencyOnWeb()
    {
        var result = Types.InAssembly(typeof(RestaurantManagement.Business.Services.IOrderService).Assembly)
            .ShouldNot()
            .HaveDependencyOn(WebNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Business layer must not depend on Web layer.");
    }

    [Fact]
    public void DataAccess_ShouldNotHaveDependencyOnBusinessOrWeb()
    {
        var result = Types.InAssembly(typeof(RestaurantManagement.DataAccess.Context.RestaurantDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(BusinessNamespace, WebNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "DataAccess layer must not depend on Business or Web.");
    }

    [Fact]
    public void WebControllers_ShouldNotDirectlyDependOnDataAccess()
    {
        var result = Types.InAssembly(typeof(RestaurantManagement.Web.Controllers.HomeController).Assembly)
            .That()
            .HaveNameEndingWith("Controller")
            .ShouldNot()
            .HaveDependencyOn(DataAccessNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Web controllers must not directly depend on DataAccess layer.");
    }
}
