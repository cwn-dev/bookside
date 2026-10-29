namespace Bookside.Core.Vocabulary.Services;

/// <summary>
/// Which words a practice round shows. Practice answers aren't recorded.
/// </summary>
public enum PracticeSet
{
    /// <summary>
    /// Words graded "Forgot" today.
    /// </summary>
    ForgottenToday,

    /// <summary>
    /// A random selection of words with a definition.
    /// </summary>
    Random,

    /// <summary>
    /// Words forgotten most often, hardest first.
    /// </summary>
    MostForgotten,
}
