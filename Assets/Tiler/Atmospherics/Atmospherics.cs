using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Tiler.Jobs;

namespace Tiler
{
    public class Atmospherics : IDisposable
    {
        public class Chunk : IDisposable
        {
            public Chunk(int size)
            {
                Gas = new NativeArray<Gas.Container>[2];
                Gas[0] = new NativeArray<Gas.Container>(size * size, Allocator.Persistent);
                Gas[1] = new NativeArray<Gas.Container>(size * size, Allocator.Persistent);


                Flow = new NativeArray<Flow>(size * size, Allocator.Persistent);
                Wall = new NativeArray<Flow>(size * size, Allocator.Persistent);

                for (int i = 0; i < size * size; i++)
                {
                    Flow[i] = Atmospherics.Flow.All;
                }

                Pressure = new NativeArray<double>(size * size, Allocator.Persistent);
            }

            public NativeArray<Gas.Container>[] Gas;
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
            None = 0,
            North = 1,
            South = 2,
            East = 4,
            West = 8,
            All = 15
        }

        public Atmospherics(int chunkSize)
        {
            size = chunkSize;
        }

        internal Dictionary<(int x, int y), Chunk> _chunks = new Dictionary<(int x, int y), Chunk>();
        internal bool flag;
        internal int size;

        public long TotalMoles;

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

        internal void AddChunk(int x, int y, Chunk chunk)
        {
            if (!_chunks.ContainsKey((x, y)))
            {
                _chunks.Add((x, y), chunk);
            }
        }

        public void ChangeTile(Flow blocking, int x, int y)
        {
            var chunkCoordinate = ChunkCoordinate(x, y);
            ActivateChunk(chunkCoordinate.Item1, chunkCoordinate.Item2);

            var chunk = _chunks[chunkCoordinate];

            int localX = x - (chunkCoordinate.Item1 * size);
            int localY = (size - 1) - (y - (chunkCoordinate.Item2 * size));

            var wall = blocking;
            chunk.Wall[localX + localY * size] = wall;

            RefreshTile(x, y);
            RefreshTile(x + 1, y);
            RefreshTile(x - 1, y);
            RefreshTile(x, y + 1);
            RefreshTile(x, y - 1);
        }


        private void RefreshTile(int x, int y)
        {
            var chunkCoordinate = ChunkCoordinate(x, y);
            ActivateChunk(chunkCoordinate.Item1, chunkCoordinate.Item2);
            var chunk = _chunks[chunkCoordinate];

            int localX = x - (chunkCoordinate.Item1 * size);
            int localY = (size - 1) - (y - (chunkCoordinate.Item2 * size));

            var wall = chunk.Wall[localX + localY * size];

            var flow = Flow.None;

            if (!FlagsHelper.IsSet(wall, Flow.East) && !FlagsHelper.IsSet(GetWall(x - 1, y), Flow.West))
            {
                FlagsHelper.Set(ref flow, Flow.East);
            }
            
            if (!FlagsHelper.IsSet(wall, Flow.West) && !FlagsHelper.IsSet(GetWall(x + 1, y), Flow.East))
            {
                FlagsHelper.Set(ref flow, Flow.West);
            }
            
            if (!FlagsHelper.IsSet(wall, Flow.North) && !FlagsHelper.IsSet(GetWall(x, y - 1), Flow.South))
            {
                FlagsHelper.Set(ref flow, Flow.North);
            }
            
            if (!FlagsHelper.IsSet(wall, Flow.South) && !FlagsHelper.IsSet(GetWall(x, y + 1), Flow.North))
            {
                FlagsHelper.Set(ref flow, Flow.South);
            }

            chunk.Flow[localX + localY * size] = flow;
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

        internal void InitializeAllChunks(Map map)
        {
            foreach (var chunk in map._chunks)
            {
                for (int i = 0; i < size * size; i++)
                {
                    var tile = chunk.Value.Tiles[i];
                    int x = i % size;
                    int y = (size - 1) - (i / size);

                    if (tile.Type == Map.Tile.TileType.Wall)
                    {
                        map.ChangeTile(tile, chunk.Key.x * size + x,  chunk.Key.y * size + y);
                    }
                }
            }

            foreach (var chunk in _chunks)
            {
                for (int i = 0; i < size * size; i++)
                {
                    chunk.Value.Pressure[i] = Gas.Pressure(chunk.Value.Gas[0][i], Gas.TileVolume);
                }
            }
        }

        public void Update()
        {
            NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(_chunks.Count, Allocator.Temp);
            int i = 0;

            List<(int, int)> _chunksToAdd = new List<(int, int)>();

            foreach (var chunk in _chunks)
            {
                var middle = chunk.Value.Gas[Convert.ToInt32(flag)];

                NativeArray<Gas.Container> east;
                if (_chunks.ContainsKey((chunk.Key.x + 1, chunk.Key.y)))
                    east = _chunks[(chunk.Key.x + 1, chunk.Key.y)].Gas[Convert.ToInt32(flag)];
                else
                    east = middle;

                NativeArray<Gas.Container> west;
                if (_chunks.ContainsKey((chunk.Key.x - 1, chunk.Key.y)))
                    west = _chunks[(chunk.Key.x - 1, chunk.Key.y)].Gas[Convert.ToInt32(flag)];
                else
                    west = middle;

                NativeArray<Gas.Container> north;
                if (_chunks.ContainsKey((chunk.Key.x, chunk.Key.y + 1)))
                    north = _chunks[(chunk.Key.x, chunk.Key.y + 1)].Gas[Convert.ToInt32(flag)];
                else
                    north = middle;

                NativeArray<Gas.Container> south;
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
                    SpreadFactor = 0.25f,
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

            var gasToAbsorb = new Gas.Container()
            {
                Mix = mix,
                Temperature = temperature
            };

            Gas.Absorb(ref gas, gas, gasToAbsorb, Gas.TileVolume, 1f);

            chunk.Gas[0][localX + localY * size] = gas;
            chunk.Gas[1][localX + localY * size] = gas;
        }

        public Gas.Container GetGas(int x, int y)
        {
            var chunkCoordinate = ChunkCoordinate(x, y);

            if (!_chunks.ContainsKey(chunkCoordinate))
                return new Gas.Container();

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

            return chunk.Flow[localX + localY * size];
        }
        
        public Flow GetWall(int x, int y)
        {
            var chunkCoordinate = ChunkCoordinate(x, y);

            if (!_chunks.ContainsKey(chunkCoordinate))
                return Flow.None;

            var chunk = _chunks[chunkCoordinate];

            int localX = x - (chunkCoordinate.Item1 * size);
            int localY = (size - 1) - (y - (chunkCoordinate.Item2 * size));

            return chunk.Wall[localX + localY * size];
        }

        public void ChunkColor(Color[] colors, Action<Color[], (int, int)> chunkUpdated, Gradient gradient,
            int drawMode)
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
                    }
                    else if(wall == Flow.None)
                    {
                        colors[i] = Color.gray;
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
}