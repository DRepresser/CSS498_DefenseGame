using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TacticalDefenseGame.Managers
{
    public class TelemetryManager
    {
        private List<WaveData> _history = new();
        private WaveData _currentWave;

        public void StartWave(int waveNumber)
        {
            _currentWave = new WaveData
            {
                WaveNumber = waveNumber,
                StartTime = DateTime.Now
            };
        }

        public void RecordEnergySpent(float amount)
        {
            if (_currentWave != null)
                _currentWave.EnergySpent += amount;
        }

        public void RecordDamageTaken(float amount)
        {
            if (_currentWave != null)
                _currentWave.DamageTaken += amount;
        }
        
        public void EndWave(float coreHpRemaining)
        {
            if (_currentWave == null) return;
            
            _currentWave.EndTime = DateTime.Now;
            _currentWave.CoreHpRemaining = coreHpRemaining;
            _history.Add(_currentWave);
            _currentWave = null;
        }

        public void ExportToJson(string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(_history, options);
                File.WriteAllText(filePath, jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Telemetry Export Failed: {ex.Message}");
            }
        }
    }

    public class WaveData
    {
        public int WaveNumber { get; set; }
        public float EnergySpent { get; set; }
        public float DamageTaken { get; set; }
        public float CoreHpRemaining { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double DurationSeconds => (EndTime - StartTime).TotalSeconds;
    }
}
