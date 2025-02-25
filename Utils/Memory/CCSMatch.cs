﻿using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using System.Runtime.InteropServices;

namespace ChaseMod.Utils.Memory;

public class CCSMatch
{
    private static nint MATCH_OFFSET = 0xEE4;

    // convar mp_default_team_winner_no_objective take func containing + 0xEE4 in param_1 and
    // value 1 in param_2, follow the last function called taking param_1 (just hope this offset doesn't change :clueless:)
    // TODO: I can probably remove this sig later
    private static MemoryFunctionVoid<IntPtr> CCSMatch_UpdateTeamScores =
        new(
            ChaseModUtils.IsLinux
                ? @"55 48 89 E5 41 57 41 56 41 55 49 89 FD BF 02 00 00 00"
                : @"\x48\x89\x5C\x24\x2A\x48\x89\x74\x24\x2A\x48\x89\x7C\x24\x2A\x41\x56\x48\x83\xEC\x20\x48\x8B\xF9\xB9\x02\x00\x00\x00"
        );
    
    [StructLayout(LayoutKind.Sequential)]
    public struct MCCSMatch
    {
        public short m_totalScore;
        public short m_actualRoundsPlayed;
        public short m_nOvertimePlaying;
        public short m_ctScoreFirstHalf;
        public short m_ctScoreSecondHalf;
        public short m_ctScoreOvertime;
        public short m_ctScoreTotal;
        public short m_terroristScoreFirstHalf;
        public short m_terroristScoreSecondHalf;
        public short m_terroristScoreOvertime;
        public short m_terroristScoreTotal;
        public short unknown;
        public int m_phase;
    }

    public static void SwapTeamScores(CCSGameRules gameRules)
    {
        var structOffset = gameRules.Handle + MATCH_OFFSET;

        var marshallMatch = Marshal.PtrToStructure<MCCSMatch>(structOffset);

        var temp = marshallMatch.m_terroristScoreFirstHalf;
        marshallMatch.m_terroristScoreFirstHalf = marshallMatch.m_ctScoreFirstHalf;
        marshallMatch.m_ctScoreFirstHalf = temp;

        temp = marshallMatch.m_terroristScoreSecondHalf;
        marshallMatch.m_terroristScoreSecondHalf = marshallMatch.m_ctScoreSecondHalf;
        marshallMatch.m_ctScoreSecondHalf = temp;

        temp = marshallMatch.m_terroristScoreOvertime;
        marshallMatch.m_terroristScoreOvertime = marshallMatch.m_ctScoreOvertime;
        marshallMatch.m_ctScoreOvertime = temp;

        temp = marshallMatch.m_terroristScoreTotal;
        marshallMatch.m_terroristScoreTotal = marshallMatch.m_ctScoreTotal;
        marshallMatch.m_ctScoreTotal = temp;

        Marshal.StructureToPtr(marshallMatch, structOffset, true);
        CCSMatch_UpdateTeamScores.Invoke(structOffset);
    }

    public static void UpdateTeamScore(CCSGameRules gameRules, CsTeam team, short score = 1)
    {
        var structOffset = gameRules.Handle + MATCH_OFFSET;

        var marshallMatch = Marshal.PtrToStructure<MCCSMatch>(structOffset);

        switch (team)
        {
            case CsTeam.Terrorist:
            {
                if (marshallMatch.m_terroristScoreSecondHalf > 0)
                    marshallMatch.m_terroristScoreSecondHalf += score;

                else marshallMatch.m_terroristScoreFirstHalf += score;

                marshallMatch.m_terroristScoreTotal += score;

                break;
            }
            case CsTeam.CounterTerrorist:
            {
                if (marshallMatch.m_ctScoreSecondHalf > 0)
                    marshallMatch.m_ctScoreSecondHalf += score;

                else marshallMatch.m_ctScoreFirstHalf += score;

                marshallMatch.m_ctScoreTotal += score;

                break;
            }
        }

        Marshal.StructureToPtr(marshallMatch, structOffset, true);
        CCSMatch_UpdateTeamScores.Invoke(structOffset);
    }
}
