using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MeowSci.SteelyEyedMissileKittenLib.Events;

namespace MeowSci.SteelyEyedMissileKittenLib.Persistence;

/// <summary>Manages the SQLite database for flight events and mission progress.</summary>
public sealed class EventDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public EventDatabase(string databasePath)
    {
        // Required in plugin/mod environments where the automatic startup hook does not run.
        SQLitePCL.Batteries_V2.Init();

        var dir = Path.GetDirectoryName(databasePath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Console.WriteLine($"[EventDatabase] Created directory: {dir}");
        }

        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
        Console.WriteLine($"[EventDatabase] Opened database: {databasePath}");
    }

    public void Initialize()
    {
        try
        {
            ExecuteNonQuery(DatabaseSchema.CreateSchemaVersionTable);
            // CreateFlightEventsTable contains multiple statements; execute each separately
            foreach (var stmt in SplitStatements(DatabaseSchema.CreateFlightEventsTable))
                ExecuteNonQuery(stmt);
            ExecuteNonQuery(DatabaseSchema.CreateMissionProgressTable);

            // Record schema version if not already present
            using var cmd = new SqliteCommand(
                "INSERT OR IGNORE INTO schema_version (version) VALUES (@v);", _connection);
            cmd.Parameters.AddWithValue("@v", DatabaseSchema.CurrentVersion);
            cmd.ExecuteNonQuery();

            Console.WriteLine("[EventDatabase] Schema initialized.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventDatabase] Initialize error: {ex.Message}");
        }
    }

    public void InsertEvent(FlightEvent evt)
    {
        try
        {
            const string sql = @"
                INSERT INTO flight_events
                    (event_type, vehicle_id, vehicle_name, timestamp_sec, parent_body_id, description, details_json)
                VALUES
                    (@event_type, @vehicle_id, @vehicle_name, @timestamp_sec, @parent_body_id, @description, @details_json);";

            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@event_type", evt.Type.ToString());
            cmd.Parameters.AddWithValue("@vehicle_id", evt.VehicleId);
            cmd.Parameters.AddWithValue("@vehicle_name", evt.VehicleName);
            cmd.Parameters.AddWithValue("@timestamp_sec", evt.TimestampSec);
            cmd.Parameters.AddWithValue("@parent_body_id", evt.ParentBodyId);
            cmd.Parameters.AddWithValue("@description", evt.Description);
            cmd.Parameters.AddWithValue("@details_json",
                evt.Details.Count > 0 ? JsonSerializer.Serialize(evt.Details) : (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventDatabase] InsertEvent error: {ex.Message}");
        }
    }

    public List<FlightEvent> QueryRecentEvents(int limit = 200)
    {
        var results = new List<FlightEvent>();
        try
        {
            const string sql = @"
                SELECT event_type, vehicle_id, vehicle_name, timestamp_sec, parent_body_id, description, details_json
                FROM flight_events
                ORDER BY id DESC
                LIMIT @limit;";

            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var detailsJson = reader.IsDBNull(reader.GetOrdinal("details_json"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("details_json"));

                var details = detailsJson != null
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(detailsJson) ?? new()
                    : new Dictionary<string, string>();

                results.Add(new FlightEvent
                {
                    Type = Enum.Parse<FlightEventType>(reader.GetString(reader.GetOrdinal("event_type"))),
                    VehicleId = reader.GetString(reader.GetOrdinal("vehicle_id")),
                    VehicleName = reader.GetString(reader.GetOrdinal("vehicle_name")),
                    TimestampSec = reader.GetDouble(reader.GetOrdinal("timestamp_sec")),
                    ParentBodyId = reader.GetString(reader.GetOrdinal("parent_body_id")),
                    Description = reader.GetString(reader.GetOrdinal("description")),
                    Details = details,
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventDatabase] QueryRecentEvents error: {ex.Message}");
        }
        return results;
    }

    public void SaveMissionProgress(
        string missionId,
        string vehicleId,
        string status,
        double startedAtSec,
        double? completedAtSec,
        string? progressJson)
    {
        try
        {
            const string sql = @"
                INSERT INTO mission_progress (mission_id, vehicle_id, status, started_at_sec, completed_at_sec, progress_json)
                VALUES (@mission_id, @vehicle_id, @status, @started_at_sec, @completed_at_sec, @progress_json)
                ON CONFLICT(mission_id, vehicle_id) DO UPDATE SET
                    status = excluded.status,
                    started_at_sec = excluded.started_at_sec,
                    completed_at_sec = excluded.completed_at_sec,
                    progress_json = excluded.progress_json;";

            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@mission_id", missionId);
            cmd.Parameters.AddWithValue("@vehicle_id", vehicleId);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@started_at_sec", startedAtSec);
            cmd.Parameters.AddWithValue("@completed_at_sec", completedAtSec.HasValue ? (object)completedAtSec.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@progress_json", progressJson ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventDatabase] SaveMissionProgress error: {ex.Message}");
        }
    }

    public (string status, double startedAtSec, double? completedAtSec, string? progressJson) LoadMissionProgress(
        string missionId,
        string vehicleId)
    {
        try
        {
            const string sql = @"
                SELECT status, started_at_sec, completed_at_sec, progress_json
                FROM mission_progress
                WHERE mission_id = @mission_id AND vehicle_id = @vehicle_id;";

            using var cmd = new SqliteCommand(sql, _connection);
            cmd.Parameters.AddWithValue("@mission_id", missionId);
            cmd.Parameters.AddWithValue("@vehicle_id", vehicleId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var status = reader.GetString(reader.GetOrdinal("status"));
                var startedAtSec = reader.GetDouble(reader.GetOrdinal("started_at_sec"));
                var completedAtSec = reader.IsDBNull(reader.GetOrdinal("completed_at_sec"))
                    ? (double?)null
                    : reader.GetDouble(reader.GetOrdinal("completed_at_sec"));
                var progressJson = reader.IsDBNull(reader.GetOrdinal("progress_json"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("progress_json"));
                return (status, startedAtSec, completedAtSec, progressJson);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EventDatabase] LoadMissionProgress error: {ex.Message}");
        }
        return ("active", 0.0, null, null);
    }

    public void Dispose()
    {
        try { _connection.Close(); }
        catch (Exception ex) { Console.WriteLine($"[EventDatabase] Dispose error: {ex.Message}"); }
    }

    private void ExecuteNonQuery(string sql)
    {
        using var cmd = new SqliteCommand(sql, _connection);
        cmd.ExecuteNonQuery();
    }

    private static IEnumerable<string> SplitStatements(string sql)
    {
        foreach (var part in sql.Split(';'))
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                yield return trimmed + ";";
        }
    }
}
