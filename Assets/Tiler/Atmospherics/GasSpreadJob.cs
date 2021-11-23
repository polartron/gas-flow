using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Tiler.Jobs
{
    [BurstCompile]
    public struct GasSpreadJob : IJobParallelFor
    {
        public static bool HasFlow(Atmospherics.Flow flow, Atmospherics.Flow flag)
        {
            int flagsValue = (int) flow;
            int flagValue = (int) flag;
            return (flagsValue & flagValue) != 0;
        }
        
        [ReadOnly] public NativeArray<Gas.Container> Input;
        [ReadOnly] public NativeArray<Gas.Container> East;
        [ReadOnly] public NativeArray<Gas.Container> North;
        [ReadOnly] public NativeArray<Gas.Container> South;
        [ReadOnly] public NativeArray<Gas.Container> West;
        [WriteOnly] public NativeArray<Gas.Container> Output;
        [WriteOnly] public NativeArray<double> Pressure;
        [ReadOnly] public NativeArray<Atmospherics.Flow> Flow;
        [ReadOnly] public int Size;
        [ReadOnly] public float SpreadFactor;

        public void Execute(int index)
        {
            int x = index % Size;
            int y = index / Size;

            Gas.Container east = default;

            if (x + 1 >= Size)
            {
                if (East == Input)
                {
                    east = new Gas.Container();
                }
                else
                {
                    east = East[y * Size];
                }
            }
            else
            {
                east = Input[x + 1 + y * Size];
            }

            Gas.Container west = default;

            if (x - 1 < 0)
            {
                if (West == Input)
                {
                    west = new Gas.Container();
                }
                else
                {
                    west = West[(Size - 1) + y * Size];
                }
            }
            else
            {
                west = Input[x - 1 + y * Size];
            }

            Gas.Container north = default;
            Gas.Container south = default;

            if (y + 1 >= Size)
            {
                if (South == Input)
                {
                    south = new Gas.Container();
                }
                else
                {
                    south = South[x];
                }
            }
            else
            {
                south = Input[x + (y + 1) * Size];
            }

            if (y - 1 < 0)
            {
                if (North == Input)
                {
                    north = new Gas.Container();
                }
                else
                {
                    north = North[x + (Size - 1) * Size];
                }
            }
            else
            {
                north = Input[x + (y - 1) * Size];
            }

            Gas.Container original = Input[index];
            Gas.Container mix = original;

            Atmospherics.Flow flow = Flow[index];

            if (HasFlow(flow, Atmospherics.Flow.North))
            {
                Gas.Spread(ref mix, original, south, Gas.TileVolume, SpreadFactor);
                Gas.Absorb(ref mix, original, south, Gas.TileVolume, SpreadFactor);
            }
            
            if (HasFlow(flow, Atmospherics.Flow.South))
            {
                Gas.Spread(ref mix, original, north, Gas.TileVolume, SpreadFactor);
                Gas.Absorb(ref mix, original, north, Gas.TileVolume, SpreadFactor);
            }
            
            if (HasFlow(flow, Atmospherics.Flow.East))
            {
                Gas.Spread(ref mix, original, west, Gas.TileVolume, SpreadFactor);
                Gas.Absorb(ref mix, original, west, Gas.TileVolume, SpreadFactor);
            }
            
            if (HasFlow(flow, Atmospherics.Flow.West))
            {
                Gas.Spread(ref mix, original, east, Gas.TileVolume, SpreadFactor);
                Gas.Absorb(ref mix, original, east, Gas.TileVolume, SpreadFactor);
            }

            
            
            Output[index] = mix;
            Pressure[index] = Gas.Pressure(mix, Gas.TileVolume);
        }
    }
    
    [BurstCompile]
    public struct CountMolesJob : IJob
    {
        [ReadOnly] public NativeArray<Gas.Container> Input;
        [WriteOnly] public NativeArray<long> TotalMoles;

        public void Execute()
        {
            long total = 0;
            for (int i = 0; i < Input.Length; i++)
            {
                total += Input[i].Mix.Moles;
            }

            TotalMoles[0] = total;
        }
    }
}