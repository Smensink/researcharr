namespace NzbDrone.Core.Languages
{
    public static class LanguageExtensions
    {
        public static string CanonicalizeLanguage(this string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return string.Empty;
            }

            return language.Trim().ToLowerInvariant();
        }
    }
}
