using Velqora.Specifications;
using Xunit;
namespace Velqora.UnitTests;
public sealed class DomainTests
{
    public static IEnumerable<object[]> Names => Specs.Cases.Keys.Select(name=>new object[]{name});
    [Theory]
    [MemberData(nameof(Names))]
    public void Core_specification(string name)=>Specs.Cases[name]();
}
