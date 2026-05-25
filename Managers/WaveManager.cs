using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TacticalDefenseGame.Models;

namespace TacticalDefenseGame.Managers
{
    public class WaveManager
    {
        public int CurrentWave { get; private set; } = 0;
        private float _spawnInterval = 1.5f;
        private float _spawnTimer = 0f;
        private int _enemiesToSpawn = 0;
        private int _enemiesSpawned = 0;
        
        public bool AllWavesComplete { get; private set; } = false;
        private const int MaxWaves = 10;

        public void StartNextWave()
        {
            if (CurrentWave < MaxWaves)
            {
                CurrentWave++;
                _enemiesToSpawn = 5 + (CurrentWave * 3);
                _enemiesSpawned = 0;
                _spawnTimer = 0f;
                _spawnInterval = Math.Max(0.5f, 1.5f - (CurrentWave * 0.1f));
            }
            else
            {
                AllWavesComplete = true;
            }
        }

        public bool IsWaveActive()
        {
            return _enemiesSpawned < _enemiesToSpawn;
        }

        public void Update(GameTime gameTime, EntityManager entityManager, GridManager gridManager, Point spawnPoint, Point corePoint)
        {
            if (AllWavesComplete) return;

            if (IsWaveActive())
            {
                _spawnTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_spawnTimer >= _spawnInterval)
                {
                    SpawnEnemy(entityManager, gridManager, spawnPoint, corePoint);
                    _spawnTimer = 0;
                }
            }
            else if (entityManager.IsWaveCleared() && CurrentWave < MaxWaves)
            {
                // Auto-start next wave after delay? For now, manual start via space is handled in main game loop
                // but we could track state here.
            }
            else if (CurrentWave == MaxWaves && entityManager.IsWaveCleared())
            {
                AllWavesComplete = true;
            }
        }

        private void SpawnEnemy(EntityManager entityManager, GridManager gridManager, Point spawnPoint, Point corePoint)
        {
            _enemiesSpawned++;
            
            // Determine type based on wave
            EnemyType type = EnemyType.Standard;
            if (CurrentWave >= 2 && _enemiesSpawned % 5 == 0) type = EnemyType.Speedster;
            if (CurrentWave >= 3 && _enemiesSpawned % 7 == 0) type = EnemyType.Tank;
            if (CurrentWave >= 4 && _enemiesSpawned % 9 == 0) type = EnemyType.Phaser;
            if (CurrentWave >= 3 && _enemiesSpawned % 11 == 0) type = EnemyType.Striker;
            if (CurrentWave >= 4 && _enemiesSpawned % 13 == 0) type = EnemyType.Support;

            var path = gridManager.FindPath(spawnPoint, corePoint);
            Vector2 startPos = new Vector2(
                spawnPoint.X * GridManager.CellSize + GridManager.CellSize / 2,
                spawnPoint.Y * GridManager.CellSize + GridManager.CellSize / 2
            );
            Vector2 corePos = new Vector2(
                corePoint.X * GridManager.CellSize + GridManager.CellSize / 2,
                corePoint.Y * GridManager.CellSize + GridManager.CellSize / 2
            );

            entityManager.SpawnEnemy(type, startPos, path, corePos, corePoint);
        }
    }
}
