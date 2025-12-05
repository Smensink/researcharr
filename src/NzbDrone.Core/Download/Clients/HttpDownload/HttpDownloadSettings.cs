using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace NzbDrone.Core.Download.Clients.HttpDownload
{
    public class HttpDownloadSettings : IProviderConfig
    {
        private static readonly HttpDownloadSettingsValidator Validator = new HttpDownloadSettingsValidator();

        [FieldDefinition(0, Label = "Download Folder", HelpText = "The folder where files will be downloaded to")]
        public string DownloadFolder { get; set; }

        [FieldDefinition(1, Label = "User Agent", HelpText = "User Agent string to use for downloads", Type = FieldType.Textbox)]
        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";

        [FieldDefinition(2, Label = "Custom Headers", HelpText = "Additional HTTP headers in format: Header1:Value1,Header2:Value2 (e.g., Referer:https://example.com,Accept-Language:en-US,en)", Type = FieldType.Textbox, Advanced = true)]
        public string CustomHeaders { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public class HttpDownloadSettingsValidator : AbstractValidator<HttpDownloadSettings>
    {
        public HttpDownloadSettingsValidator()
        {
            RuleFor(c => c.DownloadFolder).IsValidPath();
        }
    }
}
