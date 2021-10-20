using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using emotitron.Compression;
using Unity.Collections;
using Unity.Entities.Serialization;
using UnityEngine;
using BinaryWriter = Unity.Entities.Serialization.BinaryWriter;

namespace Tiler
{
    public class Map : IDisposable
    {
        internal readonly int _chunkSize;
        private Atmospherics _atmospherics;

        internal Dictionary<(int x, int y), Chunk> _chunks =
            new Dictionary<(int x, int y), Chunk>();

        public Map(int chunkSize, Atmospherics atmospherics)
        {
            _chunkSize = chunkSize;
            _atmospherics = atmospherics;
        }

        public void Dispose()
        {
            foreach (var chunk in _chunks)
            {
                chunk.Value.Dispose();
            }
        }

        private (int, int) ChunkCoordinate(int x, int y)
        {
            return (Mathf.FloorToInt((float) x / _chunkSize), Mathf.FloorToInt((float) y / _chunkSize));
        }

        private void ActivateChunk(int x, int y)
        {
            if (!_chunks.ContainsKey((x, y)))
            {
                _chunks.Add((x, y), new Chunk(_chunkSize));
            }
        }
        
        internal void AddChunk(int x, int y, Chunk chunk)
        {
            if (!_chunks.ContainsKey((x, y)))
            {
                _chunks.Add((x, y), chunk);
            }
        }

        public void ChangeTile(Tile tile, int x, int y)
        {
            var chunkCoordinate = ChunkCoordinate(x, y);

            ActivateChunk(chunkCoordinate.Item1, chunkCoordinate.Item2);

            var chunk = _chunks[chunkCoordinate];

            int localX = x - (chunkCoordinate.Item1 * _chunkSize);
            int localY = (_chunkSize - 1) - (y - (chunkCoordinate.Item2 * _chunkSize));

            chunk.Tiles[localX + localY * _chunkSize] = tile;

            bool wall = tile.Type == Tile.TileType.Wall;
            
            if (wall)
            {
                _atmospherics.ChangeTile(Atmospherics.Flow.All, x, y);
            }
            else
            {
                _atmospherics.ChangeTile(Atmospherics.Flow.None, x, y);
            }
        }

        public struct Tile
        {
            public enum TileType
            {
                Space,
                Floor,
                Wall
            }

            public TileType Type;
        }

        public class Chunk : IDisposable
        {
            public NativeArray<Tile> Tiles;

            public Chunk(int size)
            {
                Tiles = new NativeArray<Tile>(size * size, Allocator.Persistent);
            }

            public void Dispose()
            {
                Tiles.Dispose();
            }
        }
    }
}