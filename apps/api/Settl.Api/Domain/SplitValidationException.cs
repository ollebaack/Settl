namespace Settl.Api.Domain;

/// <summary>
/// Thrown by <see cref="SplitCalculator"/> when a percent/amount split is out of tolerance.
/// Carries the exact Swedish <see cref="System.Exception.Message"/> for the 400 ProblemDetails detail.
/// Pure domain concern — no HTTP dependency; endpoints translate it to a 400.
/// </summary>
public class SplitValidationException(string message) : Exception(message);
