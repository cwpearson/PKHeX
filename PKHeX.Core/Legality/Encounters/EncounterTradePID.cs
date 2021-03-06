﻿namespace PKHeX.Core
{
    /// <summary>
    /// Trade Encounter data with a fixed PID.
    /// </summary>
    public sealed class EncounterTradePID : EncounterTrade
    {
        public uint PID;
        public override Shiny Shiny { get; set; } = Shiny.FixedValue;
    }
}