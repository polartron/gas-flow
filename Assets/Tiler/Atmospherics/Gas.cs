using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using UnityEngine;

namespace Tiler
{
    public static class Gas
    {
        public static readonly double GasConstant = 831;
        public static readonly double StandardPressure = 101325;
        public static readonly int TileVolume = 2500;
        public static readonly double StandardMoles0CTile = (StandardPressure * TileVolume) / (293.15 * GasConstant);

        [BurstCompile]
        public struct Container
        {
            public Mix Mix;
            public double Temperature;
        }

        [BurstCompile]
        public struct Mix
        {
            public long Moles => Oxygen + Nitrogen + CarbonDioxide + NitrousOxide + Plasma;
            public int Oxygen;
            public int Nitrogen;
            public int CarbonDioxide;
            public int NitrousOxide;
            public int Plasma;
        }

        public static double Pressure(in Container container, float volume)
        {
            return (container.Mix.Moles * container.Temperature * GasConstant) / volume;
        }

        private static int Moles(in Container container, double pressure, float volume)
        {
            double f = container.Temperature * GasConstant;
            return (int) (volume * pressure / f);
        }

        //Remove gas from source based on pressure difference between source and target
        public static void Spread(ref Gas.Container source, in Gas.Container original, in Gas.Container target,
            float volume, float percentage)
        {
            double difference = Pressure(original, volume) - Pressure(target, volume);

            if (difference > 0)
            {
                int moles = Gas.Moles(source, difference * percentage, volume);

                if (moles == 0)
                    return;

                long sourceMoles = source.Mix.Moles;

                double oxygenContribution = (double) source.Mix.Oxygen / (double) sourceMoles;
                double nitrogenContribution = (double) source.Mix.Nitrogen / (double) sourceMoles;
                double carbonDioxideContribution = (double) source.Mix.CarbonDioxide / (double) sourceMoles;
                double nitrousOxideContribution = (double) source.Mix.NitrousOxide / (double) sourceMoles;
                double plasmaContribution = (double) source.Mix.Plasma / (double) sourceMoles;

                int oxygenTransfer = (int) ((float) (moles * oxygenContribution));
                int nitrogenTransfer = (int) ((float) (moles * nitrogenContribution));
                int carbonDioxideTransfer = (int) ((float) (moles * carbonDioxideContribution));
                int nitrousOxideTransfer = (int) ((float) (moles * nitrousOxideContribution));
                int plasmaTransfer = (int) ((float) (moles * plasmaContribution));

                int totalTransfer = oxygenTransfer + nitrogenTransfer + carbonDioxideTransfer + nitrousOxideTransfer +
                                    plasmaTransfer;


                if (totalTransfer != moles)
                {
                    //There will be rounding issues.
                    //Fix them manually

                    int offset = moles - totalTransfer;
                    int amount = Math.Abs(offset);

                    if (oxygenTransfer + offset > 0 && oxygenTransfer + offset <= source.Mix.Oxygen)
                    {
                        oxygenTransfer += offset;
                    }
                    else if (nitrogenTransfer + offset > 0 && nitrogenTransfer + offset <= source.Mix.Nitrogen)
                    {
                        nitrogenTransfer += offset;
                    }
                    else if (carbonDioxideTransfer + offset > 0 &&
                             carbonDioxideTransfer + offset <= source.Mix.CarbonDioxide)
                    {
                        carbonDioxideTransfer += offset;
                    }
                    else if (nitrousOxideTransfer + offset > 0 &&
                             nitrousOxideTransfer + offset <= source.Mix.NitrousOxide)
                    {
                        nitrousOxideTransfer += offset;
                    }
                    else if (plasmaTransfer + offset > 0 && plasmaTransfer + offset <= source.Mix.Plasma)
                    {
                        plasmaTransfer += offset;
                    }

                    if (oxygenTransfer + nitrogenTransfer + carbonDioxideTransfer + nitrousOxideTransfer +
                        plasmaTransfer != moles)
                    {
                        Debug.Log("Error");
                    }
                }

                var mix = source.Mix;

                mix.Oxygen -= oxygenTransfer;
                mix.Nitrogen -= nitrogenTransfer;
                mix.CarbonDioxide -= carbonDioxideTransfer;
                mix.NitrousOxide -= nitrousOxideTransfer;
                mix.Plasma -= plasmaTransfer;

                source.Mix = mix;
            }
        }


        public static void Absorb(ref Gas.Container source, in Gas.Container original, in Gas.Container target,
            float volume, float percentage)
        {
            double difference = Pressure(target, volume) - Pressure(original, volume);

            if (difference > 0)
            {
                int moles = Gas.Moles(target, difference * percentage, volume);

                if (moles == 0)
                    return;

                long molesAfter = source.Mix.Moles + moles;

                double temperatureTransferScale = (double) moles / (double) molesAfter;
                double temperatureMove = (target.Temperature - source.Temperature) * temperatureTransferScale;
                source.Temperature += temperatureMove;

                long targetMoles = target.Mix.Moles;

                double oxygenContribution = (double) target.Mix.Oxygen / (double) targetMoles;
                double nitrogenContribution = (double) target.Mix.Nitrogen / (double) targetMoles;
                double carbonDioxideContribution = (double) target.Mix.CarbonDioxide / (double) targetMoles;
                double nitrousOxideContribution = (double) target.Mix.NitrousOxide / (double) targetMoles;
                double plasmaContribution = (double) target.Mix.Plasma / (double) targetMoles;

                int oxygenTransfer = (int) ((float) (moles * oxygenContribution));
                int nitrogenTransfer = (int) ((float) (moles * nitrogenContribution));
                int carbonDioxideTransfer = (int) ((float) (moles * carbonDioxideContribution));
                int nitrousOxideTransfer = (int) ((float) (moles * nitrousOxideContribution));
                int plasmaTransfer = (int) ((float) (moles * plasmaContribution));


                int totalTransfer = oxygenTransfer + nitrogenTransfer + carbonDioxideTransfer + nitrousOxideTransfer +
                                    plasmaTransfer;

                if (totalTransfer != moles)
                {
                    int offset = moles - totalTransfer;
                    int amount = Math.Abs(offset);

                    if (oxygenTransfer + offset > 0 && oxygenTransfer + offset <= target.Mix.Oxygen)
                    {
                        oxygenTransfer += offset;
                    }
                    else if (nitrogenTransfer + offset > 0 && nitrogenTransfer + offset <= target.Mix.Nitrogen)
                    {
                        nitrogenTransfer += offset;
                    }
                    else if (carbonDioxideTransfer + offset > 0 &&
                             carbonDioxideTransfer + offset <= target.Mix.CarbonDioxide)
                    {
                        carbonDioxideTransfer += offset;
                    }
                    else if (nitrousOxideTransfer + offset > 0 &&
                             nitrousOxideTransfer + offset <= target.Mix.NitrousOxide)
                    {
                        nitrousOxideTransfer += offset;
                    }
                    else if (plasmaTransfer + offset > 0 && plasmaTransfer + offset <= target.Mix.Plasma)
                    {
                        plasmaTransfer += offset;
                    }

                    if (oxygenTransfer + nitrogenTransfer + carbonDioxideTransfer + nitrousOxideTransfer +
                        plasmaTransfer != moles)
                    {
                        Debug.Log("Error");
                    }
                }

                source.Mix.Oxygen += oxygenTransfer;
                source.Mix.Nitrogen += nitrogenTransfer;
                source.Mix.CarbonDioxide += carbonDioxideTransfer;
                source.Mix.NitrousOxide += nitrousOxideTransfer;
                source.Mix.Plasma += plasmaTransfer;
            }
        }
    }
}