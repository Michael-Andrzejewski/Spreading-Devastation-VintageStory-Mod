using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SpreadingDevastation.Services
{
    /// <summary>
    /// Result of a block transformation lookup.
    /// </summary>
    public readonly struct BlockTransformation
    {
        public readonly string DevastatedForm;
        public readonly string RegeneratesTo;
        public readonly bool IsValid;

        public BlockTransformation(string devastatedForm, string regeneratesTo)
        {
            DevastatedForm = devastatedForm;
            RegeneratesTo = regeneratesTo;
            IsValid = !string.IsNullOrEmpty(devastatedForm);
        }

        public static BlockTransformation None => new BlockTransformation(null, null);
    }

    /// <summary>
    /// Handles block type transformations between normal, devastated, and healed states.
    /// Follows Open/Closed principle - extend by adding new rules without modifying existing code.
    /// </summary>
    public class BlockTransformationService
    {
        private readonly ICoreServerAPI _api;
        private readonly List<IBlockTransformationRule> _devastationRules;
        private readonly List<IBlockHealingRule> _healingRules;
        private readonly HashSet<string> _devastatedPrefixes;

        public BlockTransformationService(ICoreServerAPI api)
        {
            _api = api;
            _devastationRules = new List<IBlockTransformationRule>();
            _healingRules = new List<IBlockHealingRule>();
            _devastatedPrefixes = new HashSet<string>
            {
                "devastatedsoil-",
                "drock",
                "devastationgrowth-",
                "devgrowth-"
            };

            InitializeDefaultRules();
        }

        private void InitializeDefaultRules()
        {
            // Devastation rules (order matters - first match wins)
            _devastationRules.Add(new PrefixTransformationRule("soil-", "devastatedsoil-0", "soil-verylow-none"));
            _devastationRules.Add(new PrefixTransformationRule("rock-", "drock", "rock-obsidian"));
            _devastationRules.Add(new PrefixTransformationRule("tallgrass-", "devastationgrowth-normal", "none"));
            _devastationRules.Add(new PrefixTransformationRule("smallberrybush-", "devgrowth-thorns", "leavesbranchy-grown-oak"));
            _devastationRules.Add(new PrefixTransformationRule("largeberrybush-", "devgrowth-thorns", "leavesbranchy-grown-oak"));
            _devastationRules.Add(new PrefixTransformationRule("flower-", "devgrowth-shrike", "none"));
            _devastationRules.Add(new PrefixTransformationRule("fern-", "devgrowth-shrike", "none"));
            _devastationRules.Add(new PrefixTransformationRule("crop-", "devgrowth-shard", "none"));
            _devastationRules.Add(new PrefixTransformationRule("leavesbranchy-", "devgrowth-bush", "none"));
            _devastationRules.Add(new PrefixTransformationRule("leaves-", "devgrowth-bush", "none"));
            _devastationRules.Add(new PrefixTransformationRule("gravel-", "devastatedsoil-1", "sludgygravel"));
            _devastationRules.Add(new PrefixTransformationRule("sand-", "devastatedsoil-2", "sludgygravel"));
            _devastationRules.Add(new PrefixTransformationRule("log-", "devastatedsoil-3", "log-grown-aged-ud"));

            // Healing rules
            _healingRules.Add(new PrefixHealingRule("devastatedsoil-0", "soil-verylow-none"));
            _healingRules.Add(new PrefixHealingRule("devastatedsoil-1", "sludgygravel"));
            _healingRules.Add(new PrefixHealingRule("devastatedsoil-2", "sludgygravel"));
            _healingRules.Add(new PrefixHealingRule("devastatedsoil-3", "log-grown-aged-ud"));
            _healingRules.Add(new PrefixHealingRule("drock", "rock-obsidian"));
            _healingRules.Add(new PrefixHealingRule("devastationgrowth-", "none"));
            _healingRules.Add(new PrefixHealingRule("devgrowth-shrike", "none"));
            _healingRules.Add(new PrefixHealingRule("devgrowth-shard", "none"));
            _healingRules.Add(new PrefixHealingRule("devgrowth-bush", "none"));
            _healingRules.Add(new PrefixHealingRule("devgrowth-thorns", "leavesbranchy-grown-oak"));
        }

        /// <summary>
        /// Gets the devastated form and regeneration info for a block.
        /// </summary>
        public BlockTransformation GetDevastatedForm(Block block)
        {
            if (block?.Code == null) return BlockTransformation.None;
            
            string path = block.Code.Path;

            foreach (var rule in _devastationRules)
            {
                if (rule.Matches(path))
                {
                    return rule.GetTransformation();
                }
            }

            return BlockTransformation.None;
        }

        /// <summary>
        /// Gets the healed form for a devastated block.
        /// </summary>
        public string GetHealedForm(Block block)
        {
            if (block?.Code == null) return null;
            
            string path = block.Code.Path;

            foreach (var rule in _healingRules)
            {
                if (rule.Matches(path))
                {
                    return rule.HealedForm;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a block is already devastated.
        /// </summary>
        public bool IsDevastated(Block block)
        {
            if (block?.Code == null) return false;
            
            string path = block.Code.Path;
            foreach (var prefix in _devastatedPrefixes)
            {
                if (path.StartsWith(prefix)) return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a block by its code path.
        /// </summary>
        public Block GetBlock(string codePath)
        {
            return _api.World.GetBlock(new AssetLocation("game", codePath));
        }
    }

    #region Transformation Rules

    public interface IBlockTransformationRule
    {
        bool Matches(string blockPath);
        BlockTransformation GetTransformation();
    }

    public interface IBlockHealingRule
    {
        bool Matches(string blockPath);
        string HealedForm { get; }
    }

    /// <summary>
    /// A transformation rule that matches blocks by prefix.
    /// </summary>
    public class PrefixTransformationRule : IBlockTransformationRule
    {
        private readonly string _prefix;
        private readonly BlockTransformation _transformation;

        public PrefixTransformationRule(string prefix, string devastatedForm, string regeneratesTo)
        {
            _prefix = prefix;
            _transformation = new BlockTransformation(devastatedForm, regeneratesTo);
        }

        public bool Matches(string blockPath) => blockPath.StartsWith(_prefix);
        public BlockTransformation GetTransformation() => _transformation;
    }

    /// <summary>
    /// A healing rule that matches devastated blocks by prefix.
    /// </summary>
    public class PrefixHealingRule : IBlockHealingRule
    {
        private readonly string _prefix;
        public string HealedForm { get; }

        public PrefixHealingRule(string prefix, string healedForm)
        {
            _prefix = prefix;
            HealedForm = healedForm;
        }

        public bool Matches(string blockPath) => blockPath.StartsWith(_prefix);
    }

    #endregion
}

