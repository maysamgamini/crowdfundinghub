namespace CrowdFunding.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Represents a monetary value with a normalized currency code.
/// </summary>
public sealed class Money : IEquatable<Money>
{
    public decimal Amount { get; }
    public string Currency { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Money"/> value object.
    /// </summary>
    /// <param name="amount">The non-negative decimal amount, rounded to two decimal places.</param>
    /// <param name="currency">The 3-letter ISO currency code.</param>
    /// <exception cref="ArgumentException">Thrown when amount is negative or currency is invalid.</exception>
    public Money(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        if (currency.Trim().Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Creates a zero-value <see cref="Money"/> instance for the specified currency.
    /// </summary>
    /// <param name="currency">The 3-letter ISO currency code.</param>
    /// <returns>A new <see cref="Money"/> instance with an amount of zero.</returns>
    public static Money Zero(string currency) => new(0m, currency);

    /// <summary>
    /// Adds another <see cref="Money"/> value of matching currency.
    /// </summary>
    /// <param name="other">The money to add.</param>
    /// <returns>A new <see cref="Money"/> instance representing the sum.</returns>
    /// <exception cref="InvalidOperationException">Thrown when currencies do not match.</exception>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>
    /// Subtracts another <see cref="Money"/> value of matching currency.
    /// </summary>
    /// <param name="other">The money to subtract.</param>
    /// <returns>A new <see cref="Money"/> instance representing the difference.</returns>
    /// <exception cref="InvalidOperationException">Thrown when currencies mismatch or result would be negative.</exception>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);

        if (other.Amount > Amount)
        {
            throw new InvalidOperationException("Cannot subtract more money than available.");
        }

        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        if (!Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Money currency mismatch.");
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="Money"/> object is equal to the current instance.
    /// </summary>
    /// <param name="other">The money instance to compare.</param>
    /// <returns>True if both amount and currency match; otherwise false.</returns>
    public bool Equals(Money? other)
    {
        if (other is null)
        {
            return false;
        }

        return Amount == other.Amount && Currency == other.Currency;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Money);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    /// <inheritdoc/>
    public override string ToString() => $"{Amount:0.00} {Currency}";
}
