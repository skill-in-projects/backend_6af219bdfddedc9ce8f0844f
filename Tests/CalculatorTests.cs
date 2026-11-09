using Backend.Helpers;

namespace Tests;

public class CalculatorTests
{
    [Fact]
    public void Add_ReturnsSumOfTwoNumbers()
    {
        Assert.Equal(5, Calculator.Add(2, 3));
    }

    [Fact]
    public void Add_WithNegativeNumbers_ReturnsCorrectSum()
    {
        Assert.Equal(-1, Calculator.Add(2, -3));
    }
}
