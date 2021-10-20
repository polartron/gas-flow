using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Tiler.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Tiler
{
    public class TileBehaviour : MonoBehaviour
    {
        private Map _map;
        private Atmospherics _atmos;
        private int Size = 16;
        private int DrawMode = 1;
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
            _map = new Map(Size, _atmos);
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

            if (Input.GetKeyDown(KeyCode.F5))
            {
                string mapName = "Test Save";
                
                Serialization.SaveMap(ref _map, Path.Combine(Application.persistentDataPath, "Save", mapName, "Map"));
                Serialization.SaveAtmospherics(ref _atmos, Path.Combine(Application.persistentDataPath, "Save", mapName, "Gas"));
            }
            
            if (Input.GetKeyDown(KeyCode.F6))
            {
                string mapName = "Test Save";
                Serialization.LoadMap(ref _map, Path.Combine(Application.persistentDataPath, "Save", mapName, "Map"));
                Serialization.LoadAtmospherics(ref _atmos, Path.Combine(Application.persistentDataPath, "Save", mapName, "Gas"));
                
                _atmos.InitializeAllChunks(_map);
            }

            if (Input.GetKey(KeyCode.V))
            {
                var tile = GetTile();
                _map.ChangeTile(new Map.Tile()
                {
                    Type = Map.Tile.TileType.Wall
                }, tile.x, tile.y);
            }
            
            if (Input.GetKey(KeyCode.B))
            {
                var tile = GetTile();
                _map.ChangeTile(new Map.Tile()
                {
                    Type = Map.Tile.TileType.Space
                }, tile.x, tile.y);
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

                var tile = GetTile();

                Debug.Log(tile.x + " : " + tile.y);

                _atmos.AddGas(mix, 293.15f, tile.x, tile.y);
            }

            _atmos.Update();
        }

        public (int x, int y) GetTile()
        {
            
            float orthoSize = Camera.main.orthographicSize;
            
            var rect = new Rect(0, 0, Size / orthoSize, Size / orthoSize);

            int tileX = (int) (Input.mousePosition.x / rect.width * Size);
            int tileY = (int) ((Screen.height - Input.mousePosition.y) / rect.height * Size);

            return (tileX, tileY);
        }

        void OnGUI()
        {
            double total = 0;

            float orthoSize = Camera.main.orthographicSize;
            
            var rect = new Rect(0, 0, Size / orthoSize, Size / orthoSize);

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
            var selectedFlow = _atmos.GetFlow(tileX, tileY);
            var selectedWall = _atmos.GetWall(tileX, tileY);
//
            GUI.Label(new Rect(20, 10, 200, 20), _atmos.TotalMoles.ToString());
            GUI.Label(new Rect(20, 20, 200, 20), tileX + " " + tileY);
            GUI.Label(new Rect(20, 30, 200, 20), "" + (int) _atmos.GetFlow(tileX, tileY));
            GUI.Label(new Rect(512, 40, 200, 200), $"Total Moles = {selectedTile.Mix.Moles}\n" +
                                                   $"Total Pressure = {String.Format("{0:0.00}", (Gas.Pressure(selectedTile, 2500) / 2500))} kPa\n" +
                                                   $"Temperature = {String.Format("{0:0.00}", selectedTile.Temperature)} K\n" +
                                                   $"Oxygen = {selectedTile.Mix.Oxygen}\n" +
                                                   $"Nitrogen = {selectedTile.Mix.Nitrogen}\n" +
                                                   $"Carbon Dioxide = {selectedTile.Mix.CarbonDioxide}\n" +
                                                   $"Nitrous Oxide = {selectedTile.Mix.NitrousOxide}\n" +
                                                   $"Plasma = {selectedTile.Mix.Plasma}");

            string flowing = "FLOW: ";
            flowing += GasSpreadJob.HasFlow(selectedFlow, Atmospherics.Flow.North) ? "N" : " ";
            flowing += GasSpreadJob.HasFlow(selectedFlow, Atmospherics.Flow.South) ? "S" : " ";
            flowing += GasSpreadJob.HasFlow(selectedFlow, Atmospherics.Flow.East) ? "E" : " ";
            flowing += GasSpreadJob.HasFlow(selectedFlow, Atmospherics.Flow.West) ? "W" : " ";
            
            GUI.Label(new Rect(20, 50, 200, 20), flowing);
            
            string blocking = "BLOCK: ";
            blocking += GasSpreadJob.HasFlow(selectedWall, Atmospherics.Flow.North) ? "N" : " ";
            blocking += GasSpreadJob.HasFlow(selectedWall, Atmospherics.Flow.South) ? "S" : " ";
            blocking += GasSpreadJob.HasFlow(selectedWall, Atmospherics.Flow.East) ? "E" : " ";
            blocking += GasSpreadJob.HasFlow(selectedWall, Atmospherics.Flow.West) ? "W" : " ";
            
            GUI.Label(new Rect(20, 60, 200, 20), blocking);
            
        }
    }
}