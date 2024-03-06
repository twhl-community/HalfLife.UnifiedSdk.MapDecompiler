using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Binding;

namespace HalfLife.UnifiedSdk.MapDecompiler.CmdLine
{
    internal sealed class DecompilerOptionsBinder : BinderBase<DecompilerOptions>
    {
        private readonly Option<bool> _applyNullToGeneratedFacesOption;

        private readonly Option<bool> _alwaysGenerateOriginBrushesOption;

        private readonly Option<bool> _mergeBrushesOption;

        private readonly Option<bool> _skipSolidSkyLeafsOption;

        private readonly Option<bool> _includeLiquidsOption;

        private readonly Option<BrushOptimization> _brushOptimizationOption;

        private readonly Option<List<string>> _triggerEntityClassNameWildcardsOption;

        public DecompilerOptionsBinder(Option<bool> applyNullToGeneratedFacesOption,
            Option<bool> alwaysGenerateOriginBrushesOption,
            Option<bool> mergeBrushesOption,
            Option<bool> skipSolidSkyLeafsOption,
            Option<bool> includeLiquidsOption,
            Option<BrushOptimization> brushOptimizationOption,
            Option<List<string>> triggerEntityClassNameWildcardsOption)
        {
            _applyNullToGeneratedFacesOption = applyNullToGeneratedFacesOption;
            _alwaysGenerateOriginBrushesOption = alwaysGenerateOriginBrushesOption;
            _mergeBrushesOption = mergeBrushesOption;
            _skipSolidSkyLeafsOption = skipSolidSkyLeafsOption;
            _includeLiquidsOption = includeLiquidsOption;
            _brushOptimizationOption = brushOptimizationOption;
            _triggerEntityClassNameWildcardsOption = triggerEntityClassNameWildcardsOption;
        }

        protected override DecompilerOptions GetBoundValue(BindingContext bindingContext)
        {
            var applyNullToGeneratedFaces = bindingContext.ParseResult.GetValueForOption(_applyNullToGeneratedFacesOption);
            var alwaysGenerateOriginBrushes = bindingContext.ParseResult.GetValueForOption(_alwaysGenerateOriginBrushesOption);
            var mergeBrushes = bindingContext.ParseResult.GetValueForOption(_mergeBrushesOption);
            var skipSolidSkyLeafs = bindingContext.ParseResult.GetValueForOption(_skipSolidSkyLeafsOption);
            var includeLiquids = bindingContext.ParseResult.GetValueForOption(_includeLiquidsOption);
            var brushOptimization = bindingContext.ParseResult.GetValueForOption(_brushOptimizationOption);
            var triggerEntityClassNameWildcards = bindingContext.ParseResult.GetValueForOption(_triggerEntityClassNameWildcardsOption);

            return new()
            {
                ApplyNullToGeneratedFaces = applyNullToGeneratedFaces,
                AlwaysGenerateOriginBrushes = alwaysGenerateOriginBrushes,
                MergeBrushes = mergeBrushes,
                SkipSolidSkyLeafs = skipSolidSkyLeafs,
                IncludeLiquids = includeLiquids,
                BrushOptimization = brushOptimization,
                TriggerEntityWildcards = triggerEntityClassNameWildcards?.ToImmutableList() ?? ImmutableList<string>.Empty,
            };
        }
    }
}
