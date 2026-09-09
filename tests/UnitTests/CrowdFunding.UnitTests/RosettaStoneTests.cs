using CrowdFunding.Samples.RosettaStone.PragmaticCqrs;

namespace CrowdFunding.UnitTests;

/// <summary>
/// Pure validation-logic tests for the TICKET-028 Rosetta Stone Tier 2 slice. Tier 2's handler
/// itself is a direct <c>DbContext</c> write with no branching logic worth unit-testing in
/// isolation from Postgres — see <c>RosettaStoneParityTests</c> in the integration suite for
/// end-to-end coverage of all three tiers against a real database.
/// </summary>
public sealed class RosettaStoneCreateCampaignCommandValidatorTests
{
    private readonly CreateCampaignCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var command = new CreateCampaignCommand(
            "A valid title",
            "A valid story that is definitely longer than twenty characters.",
            1000m,
            "USD");

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTitleIsBlank()
    {
        var command = new CreateCampaignCommand(
            string.Empty,
            "A valid story that is definitely longer than twenty characters.",
            1000m,
            "USD");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCampaignCommand.Title));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldFail_WhenTargetAmountIsNotPositive(decimal targetAmount)
    {
        var command = new CreateCampaignCommand(
            "A valid title",
            "A valid story that is definitely longer than twenty characters.",
            targetAmount,
            "USD");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCampaignCommand.TargetAmount));
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("DOLLARS")]
    public void Validate_ShouldFail_WhenCurrencyIsNotThreeLetters(string currency)
    {
        var command = new CreateCampaignCommand(
            "A valid title",
            "A valid story that is definitely longer than twenty characters.",
            1000m,
            currency);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCampaignCommand.Currency));
    }
}
