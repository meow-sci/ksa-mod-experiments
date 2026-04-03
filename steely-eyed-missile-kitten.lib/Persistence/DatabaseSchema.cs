using System;

namespace MeowSci.SteelyEyedMissileKittenLib.Persistence;

public static class DatabaseSchema
{
    public const int CurrentVersion = 1;

    public const string CreateSchemaVersionTable = @"
        CREATE TABLE IF NOT EXISTS schema_version (
            version INTEGER PRIMARY KEY
        );";

    public const string CreateFlightEventsTable = @"
        CREATE TABLE IF NOT EXISTS flight_events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            event_type TEXT NOT NULL,
            vehicle_id TEXT NOT NULL,
            vehicle_name TEXT NOT NULL,
            timestamp_sec REAL NOT NULL,
            parent_body_id TEXT NOT NULL,
            description TEXT NOT NULL,
            details_json TEXT,
            created_at TEXT DEFAULT (datetime('now'))
        );
        CREATE INDEX IF NOT EXISTS idx_events_vehicle ON flight_events(vehicle_id);
        CREATE INDEX IF NOT EXISTS idx_events_type ON flight_events(event_type);
        CREATE INDEX IF NOT EXISTS idx_events_timestamp ON flight_events(timestamp_sec);";

    public const string CreateMissionProgressTable = @"
        CREATE TABLE IF NOT EXISTS mission_progress (
            mission_id TEXT NOT NULL,
            vehicle_id TEXT NOT NULL,
            status TEXT NOT NULL DEFAULT 'active',
            started_at_sec REAL NOT NULL,
            completed_at_sec REAL,
            progress_json TEXT,
            PRIMARY KEY (mission_id, vehicle_id)
        );";
}
