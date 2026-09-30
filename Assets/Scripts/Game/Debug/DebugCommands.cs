using System;
using AgeOfSakura.Core;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Development-only actions. They go through the same wallet/production services as gameplay
    /// (with the DebugGrant reason), never around them. The menu that exposes them is disabled in release builds.
    /// </summary>
    public sealed class DebugCommands
    {
        private readonly GameSession session;
        private readonly WorldView world;
        private readonly Action restart;

        public DebugCommands(GameSession session, WorldView world, Action restart)
        {
            this.session = session;
            this.world = world;
            this.restart = restart;
        }

        /// <summary>Debug tools show in the Editor and Development Builds only, unless enabled explicitly at build time.</summary>
        public static bool Available
        {
            get
            {
#if AGE_OF_SAKURA_DEBUG_MENU
                return true;
#else
                return UnityEngine.Debug.isDebugBuild;
#endif
            }
        }

        public void AddCoins() => session.Wallet.Add(CurrencyType.Coins, 1000, CurrencyTransactionReason.DebugGrant);
        public void AddWood() => session.Wallet.Add(CurrencyType.Wood, 1000, CurrencyTransactionReason.DebugGrant);
        public void AddRice() => session.Wallet.Add(CurrencyType.Rice, 100, CurrencyTransactionReason.DebugGrant);
        public void AddDiamonds() => session.Wallet.Add(CurrencyType.Diamonds, 100, CurrencyTransactionReason.DebugGrant);
        public int FinishAllProductions() => session.Production.FinishAll();
        public void ToggleGrid() => world.Grid.DebugVisible = !world.Grid.DebugVisible;
        public void ToggleLockedArea() => world.Locked.Visible = !world.Locked.Visible;

        /// <summary>Flips English/German and rebuilds the UI from the save (checks translations and text fitting).</summary>
        public void ToggleLanguage()
        {
            Loc.Current = Loc.Current == Language.English ? Language.German : Language.English;
            restart();
        }

        public void ResetSave()
        {
            session.DeleteSave();
            restart();
        }
    }
}
