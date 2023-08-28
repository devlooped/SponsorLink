using System;

namespace Devlooped;

/// <summary>
/// Anotates the assembly with the account(s) that can be used to fund the project.
/// </summary>
/// <param name="account">Account that can be funded for the current project.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
class FundingAttribute : Attribute
{
    /// <summary>
    /// Initializes the attribute with the specified account.
    /// </summary>
    public FundingAttribute(string account) => Account = account;

    /// <summary>
    /// The account that can be funded for the current project.
    /// </summary>
    public string Account { get; }
}