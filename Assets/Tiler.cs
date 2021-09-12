using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Analytics;
using Random = System.Random;

public static class FlagsHelper
{
    public static bool IsSet<T>(T flags, T flag) where T : struct
    {
        int flagsValue = (int)(object)flags;
        int flagValue = (int)(object)flag;

        return (flagsValue & flagValue) != 0;
    }

    public static void Set<T>(ref T flags, T flag) where T : struct
    {
        int flagsValue = (int)(object)flags;
        int flagValue = (int)(object)flag;

        flags = (T)(object)(flagsValue | flagValue);
    }

    public static void Unset<T>(ref T flags, T flag) where T : struct
    {
        int flagsValue = (int)(object)flags;
        int flagValue = (int)(object)flag;

        flags = (T)(object)(flagsValue & (~flagValue));
    }
}

public static class Gas
{
    public static readonly double GasConstant = 831;
    public static readonly double StandardPressure = 101325;

    public static readonly double StandardMoles0C = (StandardPressure) / (293.15 * GasConstant);
    public static readonly double StandardMoles0CTile = (StandardPressure * 2500) / (293.15 * GasConstant);
    
    
    [BurstCompile]
    public struct Tile
    {
        public long Moles => Mix.Oxygen + Mix.Nitrogen + Mix.CarbonDioxide + Mix.NitrousOxide + Mix.Plasma;
        public Mix Mix;
        public double Temperature;
        
        public double Pressure => (Moles * Temperature * GasConstant) / 2500;
    }

    [BurstCompile]
    public struct Mix
    {
        public int Oxygen;
        public int Nitrogen;
        public int CarbonDioxide;
        public int NitrousOxide;
        public int Plasma;
    }

    

    private static int Moles(in Tile tile, double pressure)
    {
        double f = tile.Temperature * GasConstant;
        return (int) (2500 * pressure / f);
    }

    private static double Temperature(Tile tile, double pressure)
    {
        double f = tile.Moles * GasConstant;
        return pressure / f;
    }

    //Remove gas from source based on pressure difference between source and target
    public static void Spread(ref Gas.Tile source, in Gas.Tile original, in Gas.Tile target, float percentage)
    {
        double difference = original.Pressure - target.Pressure;

        if (difference > 0)
        {
            int moles = Gas.Moles(source, difference * percentage);

            if (moles == 0)
                return;

            long sourceMoles = source.Moles;

            double oxygenContribution = (double) source.Mix.Oxygen / (double) sourceMoles;
            double nitrogenContribution = (double) source.Mix.Nitrogen / (double) sourceMoles;
            double carbonDioxideContribution = (double) source.Mix.CarbonDioxide / (double) sourceMoles;
            double nitrousOxideContribution = (double) source.Mix.NitrousOxide / (double) sourceMoles;
            double plasmaContribution = (double) source.Mix.Plasma / (double) sourceMoles;

            double totalContribution = oxygenContribution + nitrogenContribution + carbonDioxideContribution +
                                       nitrousOxideContribution + plasmaContribution;

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
                else if (nitrousOxideTransfer + offset > 0 && nitrousOxideTransfer + offset <= source.Mix.NitrousOxide)
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

            source.Mix.Oxygen -= oxygenTransfer;
            source.Mix.Nitrogen -= nitrogenTransfer;
            source.Mix.CarbonDioxide -= carbonDioxideTransfer;
            source.Mix.NitrousOxide -= nitrousOxideTransfer;
            source.Mix.Plasma -= plasmaTransfer;
        }
    }


    public static void Absorb(ref Gas.Tile source, in Gas.Tile original, in Gas.Tile target, float percentage)
    {
        double difference = target.Pressure - original.Pressure;

        if (difference > 0)
        {
            int moles = Gas.Moles(target, difference * percentage);

            if (moles == 0)
                return;

            long molesAfter = source.Moles + moles;

            double temperatureTransferScale = (double) moles / (double) molesAfter;
            double temperatureMove = (target.Temperature - source.Temperature) * temperatureTransferScale;
            source.Temperature += temperatureMove;

            long targetMoles = target.Moles;

            double oxygenContribution = (double) target.Mix.Oxygen / (double) targetMoles;
            double nitrogenContribution = (double) target.Mix.Nitrogen / (double) targetMoles;
            double carbonDioxideContribution = (double) target.Mix.CarbonDioxide / (double) targetMoles;
            double nitrousOxideContribution = (double) target.Mix.NitrousOxide / (double) targetMoles;
            double plasmaContribution = (double) target.Mix.Plasma / (double) targetMoles;

            double totalContribution = oxygenContribution + nitrogenContribution + carbonDioxideContribution +
                                       nitrousOxideContribution + plasmaContribution;

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
                else if (nitrousOxideTransfer + offset > 0 && nitrousOxideTransfer + offset <= target.Mix.NitrousOxide)
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

public class Atmospherics : IDisposable
{
    public class Chunk : IDisposable
    {
        public Chunk(int size)
        {
            Gas = new NativeArray<Gas.Tile>[2];
            Gas[0] = new NativeArray<Gas.Tile>(size * size, Allocator.Persistent);
            Gas[1] = new NativeArray<Gas.Tile>(size * size, Allocator.Persistent);
            
            
            Flow = new NativeArray<Flow>(size * size, Allocator.Persistent);
            Wall = new NativeArray<Flow>(size * size, Allocator.Persistent);

            for (int i = 0; i < size * size; i++)
            {
                Flow[i] = Atmospherics.Flow.All;
            }
            
            Pressure = new NativeArray<double>(size * size, Allocator.Persistent);
        }

        public NativeArray<Gas.Tile>[] Gas;
        public NativeArray<double> Pressure;
        public NativeArray<Flow> Wall;
        public NativeArray<Flow> Flow;

        public void Dispose()
        {
            Gas[0].Dispose();
            Gas[1].Dispose();
            Pressure.Dispose();
            Flow.Dispose();
            Wall.Dispose();
        }
    }

    [Flags]
    public enum Flow : int
    {
        North = 1,
        South = 2,
        East = 4,
        West = 8,
        All = 15
    }

    private static bool HasFlow(Flow flow, Flow flag)
    {
        return (flow & flag) != 0;
    }

    [BurstCompile]
    private struct GasSpreadJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Gas.Tile> Input;
        [ReadOnly] public NativeArray<Gas.Tile> East;
        [ReadOnly] public NativeArray<Gas.Tile> North;
        [ReadOnly] public NativeArray<Gas.Tile> South;
        [ReadOnly] public NativeArray<Gas.Tile> West;
        [WriteOnly] public NativeArray<Gas.Tile> Output;
        [WriteOnly] public NativeArray<double> Pressure;
        [ReadOnly] public NativeArray<Flow> Flow;
        [ReadOnly] public int Size;
        [ReadOnly] public float SpreadFactor;

        public void Execute(int index)
        {
            int x = index % Size;
            int y = index / Size;

            Gas.Tile east = default;

            if (x + 1 >= Size)
            {
                if (East == Input)
                {
                    east = new Gas.Tile();
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

            Gas.Tile west = default;

            if (x - 1 < 0)
            {
                if (West == Input)
                {
                    west = new Gas.Tile();
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

            Gas.Tile north = default;
            Gas.Tile south = default;

            if (y + 1 >= Size)
            {
                if (South == Input)
                {
                    south = new Gas.Tile();
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
                    north = new Gas.Tile();
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

            Gas.Tile original = Input[index];
            Gas.Tile mix = original;

            Flow flow = Flow[index];
            
            if (HasFlow(flow, Atmospherics.Flow.North))
            {
                Gas.Spread(ref mix, original, south, SpreadFactor);
                Gas.Absorb(ref mix, original, south, SpreadFactor);
            }

            if (HasFlow(flow, Atmospherics.Flow.South))
            {
                Gas.Spread(ref mix, original, north, SpreadFactor);
                Gas.Absorb(ref mix, original, north, SpreadFactor);
            }

            if (HasFlow(flow, Atmospherics.Flow.East))
            {
                Gas.Spread(ref mix, original, west, SpreadFactor);
                Gas.Absorb(ref mix, original, west, SpreadFactor);
            }

            if (HasFlow(flow, Atmospherics.Flow.West))
            {
                Gas.Spread(ref mix, original, east, SpreadFactor);
                Gas.Absorb(ref mix, original, east, SpreadFactor);
            }

            Output[index] = mix;
            Pressure[index] = mix.Pressure;
        }
    }


    [BurstCompile]
    private struct CountMolesJob : IJob
    {
        [ReadOnly] public NativeArray<Gas.Tile> Input;
        [WriteOnly] public NativeArray<long> TotalMoles;

        public void Execute()
        {
            long total = 0;
            for (int i = 0; i < Input.Length; i++)
            {
                total += Input[i].Moles;
            }

            TotalMoles[0] = total;
        }
    }

    public Atmospherics(int chunkSize)
    {
        size = chunkSize;
    }

    private Dictionary<(int x, int y), Chunk> _chunks = new Dictionary<(int x, int y), Chunk>();
    private bool flag;
    private int size;

    public long TotalMoles;

    public IEnumerator<Chunk> Chunks => _chunks.Values.GetEnumerator();

    private (int, int) ChunkCoordinate(int x, int y)
    {
        return (Mathf.FloorToInt((float) x / size), Mathf.FloorToInt((float) y / size));
    }

    public void ActivateChunk(int x, int y)
    {
        if (!_chunks.ContainsKey((x, y)))
        {
            _chunks.Add((x, y), new Chunk(size));
        }
    }

    public void AddWall(Flow directions, int x, int y)
    {
        var chunkCoordinate = ChunkCoordinate(x, y);

        ActivateChunk(chunkCoordinate.Item1, chunkCoordinate.Item2);

        var chunk = _chunks[chunkCoordinate];

        int localX = x - (chunkCoordinate.Item1 * size);
        int localY = (size - 1) - (y - (chunkCoordinate.Item2 * size));

        var wall = chunk.Wall[localX + localY * size];
        FlagsHelper.Set(ref wall, directions);
        chunk.Wall[localX + localY * size] = wall;
        
        var flow = chunk.Flow[localX + localY * size];
        FlagsHelper.Unset(ref flow, directions);
        chunk.Flow[localX + localY * size] = flow;

        UpdateFlow(ref wall, x - 1, y, Flow.West);
        UpdateFlow(ref wall, x + 1, y, Flow.East);
        
        UpdateFlow(ref wall, x, y + 1, Flow.North);
        UpdateFlow(ref wall, x, y - 1, Flow.South);
    }

    private void UpdateFlow(ref Flow tile, int x, int y, Flow from)
    {
        var chunkCoordinate = ChunkCoordinate(x, y);

        ActivateChunk(chunkCoordinate.Item1, chunkCoordinate.Item2);

        var chunk = _chunks[chunkCoordinate];

        int localX = x - (chunkCoordinate.Item1 * size);
        int localY = (size - 1) - (y - (chunkCoordinate.Item2 * size));
        
        var target = chunk.Flow[localX + localY * size];

        switch (from)
        {
            case Flow.North:
                if (FlagsHelper.IsSet(tile, Flow.South))
                    FlagsHelper.Unset(ref target, Flow.North);
                else
                    FlagsHelper.Set(ref target, Flow.North);
                break;
            case Flow.South:
                if (FlagsHelper.IsSet(tile, Flow.North))
                    FlagsHelper.Unset(ref target, Flow.South);
                else
                    FlagsHelper.Set(ref target, Flow.South);
                break;
            case Flow.East:
                if (FlagsHelper.IsSet(tile, Flow.West))
                    FlagsHelper.Unset(ref target, Flow.East);
                else
                    FlagsHelper.Set(ref target, Flow.East);
                break;
            case Flow.West:
                if (FlagsHelper.IsSet(tile, Flow.East))
                    FlagsHelper.Unset(ref target, Flow.West);
                else
                    FlagsHelper.Set(ref target, Flow.West);
                break;
        }

        chunk.Flow[localX + localY * size] = target;

    }

    public void Update()
    {
        NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(_chunks.Count, Allocator.Temp);
        int i = 0;

        List<(int, int)> _chunksToAdd = new List<(int, int)>();

        foreach (var chunk in _chunks)
        {
            var middle = chunk.Value.Gas[Convert.ToInt32(flag)];

            NativeArray<Gas.Tile> east;
            if (_chunks.ContainsKey((chunk.Key.x + 1, chunk.Key.y)))
                east = _chunks[(chunk.Key.x + 1, chunk.Key.y)].Gas[Convert.ToInt32(flag)];
            else
                east = middle;

            NativeArray<Gas.Tile> west;
            if (_chunks.ContainsKey((chunk.Key.x - 1, chunk.Key.y)))
                west = _chunks[(chunk.Key.x - 1, chunk.Key.y)].Gas[Convert.ToInt32(flag)];
            else
                west = middle;

            NativeArray<Gas.Tile> north;
            if (_chunks.ContainsKey((chunk.Key.x, chunk.Key.y + 1)))
                north = _chunks[(chunk.Key.x, chunk.Key.y + 1)].Gas[Convert.ToInt32(flag)];
            else
                north = middle;

            NativeArray<Gas.Tile> south;
            if (_chunks.ContainsKey((chunk.Key.x, chunk.Key.y - 1)))
                south = _chunks[(chunk.Key.x, chunk.Key.y - 1)].Gas[Convert.ToInt32(flag)];
            else
                south = middle;

            GasSpreadJob job = new GasSpreadJob()
            {
                Input = middle,
                North = north,
                East = east,
                West = west,
                South = south,
                Output = chunk.Value.Gas[Convert.ToInt32(!flag)],
                Pressure = chunk.Value.Pressure,
                Flow = chunk.Value.Flow,
                Size = size,
                SpreadFactor = 0.175f,
            };

            jobs[i] = job.Schedule(size * size, 4);
            i++;
        }

        JobHandle.CompleteAll(jobs);
        jobs.Dispose();

        TotalMoles = 0;

        foreach (var chunk in _chunks)
        {
            CountMolesJob job = new CountMolesJob()
            {
                Input = flag ? chunk.Value.Gas[1] : chunk.Value.Gas[0],
                TotalMoles = new NativeArray<long>(1, Allocator.TempJob)
            };

            job.Run();

            TotalMoles += job.TotalMoles[0];

            if (job.TotalMoles[0] > 0)
            {
                if (!_chunks.ContainsKey((chunk.Key.x + 1, chunk.Key.y)))
                    _chunksToAdd.Add((chunk.Key.x + 1, chunk.Key.y));
                if (!_chunks.ContainsKey((chunk.Key.x - 1, chunk.Key.y)))
                    _chunksToAdd.Add((chunk.Key.x - 1, chunk.Key.y));
                if (!_chunks.ContainsKey((chunk.Key.x, chunk.Key.y + 1)))
                    _chunksToAdd.Add((chunk.Key.x, chunk.Key.y + 1));
                if (!_chunks.ContainsKey((chunk.Key.x, chunk.Key.y - 1)))
                    _chunksToAdd.Add((chunk.Key.x, chunk.Key.y - 1));
            }

            job.TotalMoles.Dispose();
        }

        for (int c = 0; c < _chunksToAdd.Count; c++)
        {
            ActivateChunk(_chunksToAdd[c].Item1, _chunksToAdd[c].Item2);
        }

        flag = !flag;
    }

    public void AddGas(Gas.Mix mix, float temperature, int x, int y)
    {
        var chunkCoordinate = ChunkCoordinate(x, y);

        ActivateChunk(chunkCoordinate.Item1, chunkCoordinate.Item2);

        var chunk = _chunks[chunkCoordinate];

        int localX = x - (chunkCoordinate.Item1 * size);
        int localY = (size - 1) - (y - (chunkCoordinate.Item2 * size));

        var gas = chunk.Gas[Convert.ToInt32(flag)][localX + localY * size];
        
        var gasToAbsorb = new Gas.Tile()
        {
            Mix = mix,
            Temperature = temperature
        };

        Gas.Absorb(ref gas, gas, gasToAbsorb, 1f);

        chunk.Gas[0][localX + localY * size] = gas;
        chunk.Gas[1][localX + localY * size] = gas;
    }

    public Gas.Tile GetGas(int x, int y)
    {
        var chunkCoordinate = ChunkCoordinate(x, y);

        if (!_chunks.ContainsKey(chunkCoordinate))
            return new Gas.Tile();

        var chunk = _chunks[chunkCoordinate];

        int localX = x - (chunkCoordinate.Item1 * size);
        int localY = (size - 1) - (y - (chunkCoordinate.Item2 * size));

        var gas = chunk.Gas[Convert.ToInt32(flag)][localX + localY * size];
        return gas;
    }
    
    public Flow GetFlow(int x, int y)
    {
        var chunkCoordinate = ChunkCoordinate(x, y);

        if (!_chunks.ContainsKey(chunkCoordinate))
            return Flow.All;

        var chunk = _chunks[chunkCoordinate];

        int localX = x - (chunkCoordinate.Item1 * size);
        int localY = (size - 1) - (y - (chunkCoordinate.Item2 * size));

        return chunk.Flow[localX + localY * size];;
    }

    public void ChunkColor(Color[] colors, Action<Color[], (int, int)> chunkUpdated, Gradient gradient, int drawMode)
    {
        foreach (var chunk in _chunks)
        {
            var pressures = chunk.Value.Pressure;
            var walls = chunk.Value.Wall;
            var flows = chunk.Value.Flow;
            
            for (int i = 0; i < pressures.Length; i++)
            {
                var wall = walls[i];

                if (wall == Flow.All)
                {
                    colors[i] = Color.white;
                    continue;
                }

                if (drawMode == 1)
                {
                    var pressure = pressures[i];

                    float deviation = Mathf.InverseLerp(0, (float) Gas.StandardPressure * 4, (float) pressure);

                    if (pressure == 0)
                    {
                        colors[i] = new Color(0.1f, 0.1f, 0.1f, 1f);
                    }
                    else
                    {
                        colors[i] = gradient.Evaluate(deviation);
                    }
                }
                else if (drawMode == 2)
                {
                    var flow = flows[i];
                    int a = (int) flow;

                    colors[i] = Color.HSVToRGB((float) a / (int) Flow.All, 1f, 1f);
                }
                
                
            }

            chunkUpdated.Invoke(colors, (chunk.Key));
        }
    }

    public void Dispose()
    {
        foreach (var chunk in _chunks)
            chunk.Value.Dispose();
    }
}

public class Tiler : MonoBehaviour
{
    private Atmospherics _atmos;
    private int Size = 16;
    private int DrawMode = 0;
    private Dictionary<(int, int), Texture2D> _textures = new Dictionary<(int, int), Texture2D>();
    [SerializeField] private Gradient gradient;

    private void OnDestroy()
    {
        _atmos.Dispose();
    }

    // Start is called before the first frame update
    void Start()
    {
        _atmos = new Atmospherics(Size);

        int a = 68;
        
        for (int i = 0; i < 100; i++)
        {
            _atmos.AddWall(Atmospherics.Flow.All, a + i, a);
        }
        
        for (int i = 0; i < 100; i++)
        {
            _atmos.AddWall(Atmospherics.Flow.All, a + i, a + 32);
        }
        
        for (int i = 0; i < 35; i++)
        {
            _atmos.AddWall(Atmospherics.Flow.All, a + 32, a + i);
        }
        
        for (int i = 0; i < 35; i++)
        {
            _atmos.AddWall(Atmospherics.Flow.All, a, a + i);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            DrawMode = 1;
        }
        
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            DrawMode = 2;
        }
        
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            DrawMode = 3;
        }
        
        if (Input.GetKeyDown(KeyCode.C))
        {
            int standardMoles = (int) (Gas.StandardMoles0CTile) * 1000;

            var mix = new Gas.Mix()
            {
                Oxygen = standardMoles,
                Nitrogen = 0,
                Plasma = 0,
                CarbonDioxide = 0,
                NitrousOxide = 0
            };

            var rect = new Rect(0, 0, Size * 2, Size * 2);

            int tileX = (int) (Input.mousePosition.x / rect.width * Size);
            int tileY = (int) ((Screen.height - Input.mousePosition.y) / rect.height * Size);
            
            Debug.Log(tileX + " : " + tileY);
            
            _atmos.AddGas(mix, 293.15f, tileX, tileY);
        }

        _atmos.Update();
    }

    void OnGUI()
    {
        double total = 0;

        var rect = new Rect(0, 0, Size * 2, Size * 2);

        int tileX = (int) (Input.mousePosition.x / rect.width * Size);
        int tileY = (int) ((Screen.height - Input.mousePosition.y) / rect.height * Size);

        Color[] c = new Color[Size * Size];
        
        _atmos.ChunkColor(c, delegate(Color[] colors, (int, int) coordinates)
        {
            if (!_textures.ContainsKey(coordinates))
            {
                var t = new Texture2D(Size, Size, TextureFormat.ARGB32, false);
                t.filterMode = FilterMode.Point;
                t.Apply();
                _textures.Add(coordinates, t);
            }

            var texture = _textures[coordinates];

            var textureRect = rect;
            textureRect.x = rect.x + coordinates.Item1 * rect.width;
            textureRect.y = rect.y + coordinates.Item2 * rect.width;
            texture.SetPixels(colors);
            texture.Apply();

            GUI.DrawTexture(textureRect, texture);
        }, gradient, DrawMode);

//
        var selectedTile = _atmos.GetGas(tileX, tileY);
//
        GUI.Label(new Rect(20, 20, 200, 20), tileX + " " + tileY);
        GUI.Label(new Rect(20, 30, 200, 20), "" + (int) _atmos.GetFlow(tileX, tileY));
        GUI.Label(new Rect(512, 40, 200, 200), $"Total Moles = {selectedTile.Moles}\n" +
                                               $"Total Pressure = {String.Format("{0:0.00}", (selectedTile.Pressure / 2500))} kPa\n" +
                                               $"Temperature = {String.Format("{0:0.00}", selectedTile.Temperature)} K\n" +
                                               $"Oxygen = {selectedTile.Mix.Oxygen}\n" +
                                               $"Nitrogen = {selectedTile.Mix.Nitrogen}\n" +
                                               $"Carbon Dioxide = {selectedTile.Mix.CarbonDioxide}\n" +
                                               $"Nitrous Oxide = {selectedTile.Mix.NitrousOxide}\n" +
                                               $"Plasma = {selectedTile.Mix.Plasma}");
        GUI.Label(new Rect(20, 10, 200, 20), _atmos.TotalMoles.ToString());
    }
}