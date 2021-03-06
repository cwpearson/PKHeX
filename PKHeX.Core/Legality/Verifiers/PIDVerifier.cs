﻿using System;
using static PKHeX.Core.LegalityCheckStrings;

namespace PKHeX.Core
{
    /// <summary>
    /// Verifies the <see cref="PKM.EncryptionConstant"/>.
    /// </summary>
    public sealed class PIDVerifier : Verifier
    {
        protected override CheckIdentifier Identifier => CheckIdentifier.PID;

        public override void Verify(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            if (pkm.Format >= 6)
                VerifyEC(data);

            var EncounterMatch = data.EncounterMatch;
            if (EncounterMatch.Species == 265)
                VerifyECPIDWurmple(data);

            if (pkm.PID == 0)
                data.AddLine(Get(LPIDZero, Severity.Fishy));
            if (pkm.Nature >= 25) // out of range
                data.AddLine(GetInvalid(LPIDNatureMismatch));

            var Info = data.Info;
            if ((Info.Generation >= 6 || (Info.Generation < 3 && pkm.Format >= 7)) && pkm.PID == pkm.EncryptionConstant)
                data.AddLine(GetInvalid(LPIDEqualsEC)); // better to flag than 1:2^32 odds since RNG is not feasible to yield match

            VerifyShiny(data);
        }

        private void VerifyShiny(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            var Info = data.Info;

            switch (data.EncounterMatch)
            {
                case EncounterStatic s:
                    if (!s.Shiny.IsValid(pkm))
                        data.AddLine(GetInvalid(LEncStaticPIDShiny, CheckIdentifier.Shiny));

                    // gen5 correlation
                    if (Info.Generation != 5)
                        break;
                    if (s.Location == 75) // Entree Forest
                        break;
                    if (s.Gift || s.Roaming || s.Ability != 4)
                        break;
                    if (s is EncounterStaticPID p && p.NSparkle)
                        break;
                    VerifyG5PID_IDCorrelation(data);
                    break;

                case EncounterSlot w:
                    if (pkm.IsShiny && w.Type == SlotType.HiddenGrotto)
                        data.AddLine(GetInvalid(LG5PIDShinyGrotto, CheckIdentifier.Shiny));
                    if (Info.Generation == 5 && w.Type != SlotType.HiddenGrotto)
                        VerifyG5PID_IDCorrelation(data);
                    break;

                case PCD d: // fixed PID
                    if (d.Gift.PK.PID != 1 && pkm.EncryptionConstant != d.Gift.PK.PID)
                        data.AddLine(GetInvalid(LEncGiftPIDMismatch, CheckIdentifier.Shiny));
                    break;
            }
        }

        private void VerifyG5PID_IDCorrelation(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            var pid = pkm.EncryptionConstant;
            var result = (pid & 1) ^ (pid >> 31) ^ (pkm.TID & 1) ^ (pkm.SID & 1);
            if (result != 0)
                data.AddLine(GetInvalid(LPIDTypeMismatch));
        }

        private void VerifyECPIDWurmple(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            uint evoVal = PKX.GetWurmpleEvoVal(pkm.EncryptionConstant);

            if (pkm.Species == 265)
            {
                var spec = evoVal == 0 ? LegalityAnalysis.SpeciesStrings[267] : LegalityAnalysis.SpeciesStrings[269];
                var msg = string.Format(L_XWurmpleEvo_0, spec);
                data.AddLine(GetValid(msg, CheckIdentifier.EC));
            }
            else if (evoVal != Array.IndexOf(Legal.WurmpleEvolutions, pkm.Species) / 2)
            {
                data.AddLine(GetInvalid(LPIDEncryptWurmple, CheckIdentifier.EC));
            }
        }

        private void VerifyEC(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            var Info = data.Info;

            if (pkm.EncryptionConstant == 0)
                data.AddLine(Get(LPIDEncryptZero, Severity.Fishy, CheckIdentifier.EC));
            if (3 <= Info.Generation && Info.Generation <= 5)
            {
                VerifyTransferEC(data);
            }
            else
            {
                int xor = pkm.TSV ^ pkm.PSV;
                if (xor < 16 && xor >= 8 && (pkm.PID ^ 0x80000000) == pkm.EncryptionConstant)
                    data.AddLine(Get(LTransferPIDECXor, Severity.Fishy, CheckIdentifier.EC));
            }
        }

        private void VerifyTransferEC(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            // When transferred to Generation 6, the Encryption Constant is copied from the PID.
            // The PID is then checked to see if it becomes shiny with the new Shiny rules (>>4 instead of >>3)
            // If the PID is nonshiny->shiny, the top bit is flipped.

            // Check to see if the PID and EC are properly configured.
            bool xorPID = ((pkm.TID ^ pkm.SID ^ (int)(pkm.PID & 0xFFFF) ^ (int)(pkm.PID >> 16)) & ~0x7) == 8;
            bool valid = xorPID
                ? pkm.EncryptionConstant == (pkm.PID ^ 0x80000000)
                : pkm.EncryptionConstant == pkm.PID;

            if (valid)
                return;

            var msg = xorPID ? LTransferPIDECBitFlip : LTransferPIDECEquals;
            data.AddLine(GetInvalid(msg, CheckIdentifier.EC));
        }
    }
}
