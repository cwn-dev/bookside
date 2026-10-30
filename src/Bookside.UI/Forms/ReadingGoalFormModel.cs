namespace Bookside.UI.Forms;

/// <summary>
/// Mutable form state for a reading goal; the property name matches the
/// service's "TargetBooks" field.
/// </summary>
public sealed class ReadingGoalFormModel
{
    public int? TargetBooks { get; set; }
}
