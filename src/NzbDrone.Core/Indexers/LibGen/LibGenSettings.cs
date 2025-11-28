using FluentValidation;
using Newtonsoft.Json;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.LibGen
{
    public class LibGenSettings : IIndexerSettings
    {
        private static readonly LibGenSettingsValidator Validator = new LibGenSettingsValidator();

        [FieldDefinition(0, Label = "Mirrors", HelpText = "List of LibGen mirrors (comma or newline separated). Try disabling FlareSolverr first - direct requests often work better with proper headers.", Type = FieldType.Textbox)]
        public string Mirrors { get; set; } = "https://libgen.is, https://libgen.rs, https://libgen.st";

        [FieldDefinition(1, Label = "FlareSolverr URL", HelpText = "Optional. If set, LibGen requests will be routed via FlareSolverr (e.g. http://flaresolverr:8191)", Type = FieldType.Textbox, Advanced = true)]
        public string FlareSolverrUrl { get; set; }

        [JsonIgnore]
        public string BaseUrl { get; set; } = "http://libgen.is";

        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public class LibGenSettingsValidator : AbstractValidator<LibGenSettings>
    {
        public LibGenSettingsValidator()
        {
            RuleFor(c => c.Mirrors).NotEmpty();
        }
    }
}
