﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

using System.Data.SQLite;

using Vocaluxe.Lib.Draw;
using Vocaluxe.Lib.Song;

namespace Vocaluxe.Base
{
    static class CDataBase
    {
        private static string _HighscoreFilePath;
        private static string _CoverFilePath;

        public static void Init()
        {
            _HighscoreFilePath = Path.Combine(System.Environment.CurrentDirectory, CSettings.sFileHighscoreDB);
            _CoverFilePath = Path.Combine(System.Environment.CurrentDirectory, CSettings.sFileCoverDB);

            InitHighscoreDB();
            InitCoverDB();
        }

        #region Highscores
        public static int AddScore(SPlayer player)
        {
            int lastInsertID = -1;

            SQLiteConnection connection = new SQLiteConnection();
            connection.ConnectionString = "Data Source=" + _HighscoreFilePath;
            SQLiteCommand command;

            try
            {
                connection.Open();
            }
            catch (Exception)
            {
                return -1;
            }

            command = new SQLiteCommand(connection);

            int DataBaseSongID = GetDataBaseSongID(player, command);
            if (DataBaseSongID >= 0)
            {

                int Medley = 0;
                if (player.Medley)
                    Medley = 1;

                int Duet = 0;
                if (player.Duet)
                    Duet = 1;
                
                command.CommandText = "INSERT INTO Scores (SongID, PlayerName, Score, LineNr, Date, Medley, Duet, Difficulty) " +
                    "VALUES (@SongID, @PlayerName, @Score, @LineNr, @Date, @Medley, @Duet, @Difficulty)";
                command.Parameters.Add("@SongID", System.Data.DbType.Int32, 0).Value = DataBaseSongID;
                command.Parameters.Add("@PlayerName", System.Data.DbType.String, 0).Value = player.Name;
                command.Parameters.Add("@Score", System.Data.DbType.Int32, 0).Value = (int)Math.Round(player.Points);
                command.Parameters.Add("@LineNr", System.Data.DbType.Int32, 0).Value = (int)player.LineNr;
                command.Parameters.Add("@Date", System.Data.DbType.Int64, 0).Value = player.DateTicks;
                command.Parameters.Add("@Medley", System.Data.DbType.Int32, 0).Value = Medley;
                command.Parameters.Add("@Duet", System.Data.DbType.Int32, 0).Value = Duet;
                command.Parameters.Add("@Difficulty", System.Data.DbType.Int32, 0).Value = (int)player.Difficulty;
                command.ExecuteNonQuery();

                //Read last insert line
                command.CommandText = "SELECT id FROM Scores ORDER BY Date DESC LIMIT 0, 1";

                SQLiteDataReader reader = null;
                try
                {
                    reader = command.ExecuteReader();
                }
                catch (Exception)
                {
                    throw;
                }

                if (reader != null && reader.HasRows)
                {
                    while (reader.Read())
                    {
                        lastInsertID = reader.GetInt32(0);
                    }

                    reader.Close();
                    reader.Dispose();
                }
            }

            command.Dispose();
            connection.Close();
            connection.Dispose();

            return lastInsertID;
        }

        public static void LoadScore(ref List<SScores> Score, SPlayer player)
        {
            SQLiteConnection connection = new SQLiteConnection();
            connection.ConnectionString = "Data Source=" + _HighscoreFilePath;
            SQLiteCommand command;

            Score = new List<SScores>();
            
            try
            {
                connection.Open();
            }
            catch (Exception)
            {
                return;
            }

            command = new SQLiteCommand(connection);

            int Medley = 0;
            if (player.Medley)
                Medley = 1;

            int Duet = 0;
            if (player.Duet)
                Duet = 1;

            int DataBaseSongID = GetDataBaseSongID(player, command);
            if (DataBaseSongID >= 0)
            {
                command.CommandText = "SELECT PlayerName, Score, Date, Difficulty, LineNr, id FROM Scores " +
                    "WHERE [SongID] = @SongID AND [Medley] = @Medley AND [Duet] = @Duet " +
                    "ORDER BY [Score] DESC";
                command.Parameters.Add("@SongID", System.Data.DbType.Int32, 0).Value = DataBaseSongID;
                command.Parameters.Add("@Medley", System.Data.DbType.Int32, 0).Value = Medley;
                command.Parameters.Add("@Duet", System.Data.DbType.Int32, 0).Value = Duet;
                
                SQLiteDataReader reader = null;
                try
                {
                    reader = command.ExecuteReader();
                }
                catch (Exception)
                {
                    throw;
                }

                if (reader != null && reader.HasRows)
                {
                    while (reader.Read())
                    {
                        SScores score = new SScores();
                        score.Name = reader.GetString(0);
                        score.Score = reader.GetInt32(1);
                        score.Date = new DateTime(reader.GetInt64(2)).ToString("dd/MM/yyyy");
                        score.Difficulty = (EGameDifficulty)reader.GetInt32(3);
                        score.LineNr = reader.GetInt32(4);
                        score.ID = reader.GetInt32(5);

                        Score.Add(score);
                    }
                    
                    reader.Close();
                    reader.Dispose();                    
                }
                
            }
            

            command.Dispose();
            connection.Close();
            connection.Dispose();
        }

        private static int GetDataBaseSongID(SPlayer player, SQLiteCommand command)
        {
            CSong song = CSongs.GetSong(player.SongID);

            if (song == null)
                return -1;

            command.CommandText = "SELECT id FROM Songs WHERE [Title] = @title AND [Artist] = @artist";
            command.Parameters.Add("@title", System.Data.DbType.String, 0).Value = song.Title;
            command.Parameters.Add("@artist", System.Data.DbType.String, 0).Value = song.Artist;

            SQLiteDataReader reader = null;
            try
            {
                reader = command.ExecuteReader();
            }
            catch (Exception)
            {
                throw;
            }

            if (reader != null && reader.HasRows)
            {
                reader.Read();
                int id = reader.GetInt32(0);
                reader.Close();
                reader.Dispose();
                return id;
            }
            else
            {
                if (reader != null)
                    reader.Close();

                command.CommandText = "INSERT INTO Songs (Title, Artist, NumPlayed) " +
                    "VALUES (@title, @artist, 0)";
                command.Parameters.Add("@title", System.Data.DbType.String, 0).Value = song.Title;
                command.Parameters.Add("@artist", System.Data.DbType.String, 0).Value = song.Artist;
                command.ExecuteNonQuery();

                command.CommandText = "SELECT id FROM Songs WHERE [Title] = @title AND [Artist] = @artist";
                command.Parameters.Add("@title", System.Data.DbType.String, 0).Value = song.Title;
                command.Parameters.Add("@artist", System.Data.DbType.String, 0).Value = song.Artist;

                reader = null;
                try
                {
                    reader = command.ExecuteReader();
                }
                catch (Exception)
                {
                    throw;
                }

                if (reader != null)
                {
                    reader.Read();
                    int id = reader.GetInt32(0);
                    reader.Close();
                    reader.Dispose();
                    return id;
                }
            }

            if (reader != null)
            {
                reader.Close();
                reader.Dispose();
            }
            
            return -1;
        }

        private static bool InitHighscoreDB()
        {
            SQLiteConnection connection = new SQLiteConnection();
            connection.ConnectionString = "Data Source=" + _HighscoreFilePath;
            SQLiteCommand command;

            try
            {
                connection.Open();
            }
            catch (Exception)
            {
                return false;
            }

            command = new SQLiteCommand(connection);
            command.CommandText = "SELECT Value FROM Version";

            SQLiteDataReader reader = null;

            try
            {
                reader = command.ExecuteReader();
            }
            catch (Exception)
            {
                ;
            }

            if (reader == null)
            {
                // create new database/tables
                CreateHighscoreDB();
            }
            else if (reader.FieldCount == 0)
            {
                // create new database/tables
                CreateHighscoreDB();
            }
            else
            {
                reader.Read();

                if (reader.GetInt32(0) < CSettings.iDatabaseHighscoreVersion)
                {
                    // update database
                }
            }

            if (reader != null)
            {
                reader.Close();
                reader.Dispose();
            }

            command.Dispose();

            connection.Close();
            connection.Dispose();

            return true;
        }

        private static void CreateHighscoreDB()
        {
            SQLiteConnection connection = new SQLiteConnection();
            connection.ConnectionString = "Data Source=" + _HighscoreFilePath;
            SQLiteCommand command;

            try
            {
                connection.Open();
            }
            catch (Exception)
            {
                return;
            }

            command = new SQLiteCommand(connection);

            command.CommandText = "CREATE TABLE IF NOT EXISTS Version ( id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Value INTEGER NOT NULL);";
            command.ExecuteNonQuery();

            command.CommandText = "INSERT INTO Version (id, Value) VALUES(NULL, " + CSettings.iDatabaseHighscoreVersion.ToString() + ")";
            command.ExecuteNonQuery();

            command.CommandText = "CREATE TABLE IF NOT EXISTS Songs ( id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                "Artist TEXT NOT NULL, Title TEXT NOT NULL, NumPlayed INTEGER);";
            command.ExecuteNonQuery();

            command.CommandText = "CREATE TABLE IF NOT EXISTS Scores ( id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                "SongID INTEGER NOT NULL, PlayerName TEXT NOT NULL, Score INTEGER NOT NULL, LineNr INTEGER NOT NULL, Date BIGINT NOT NULL, " +
                "Medley INTEGER NOT NULL, Duet INTEGER NOT NULL, Difficulty INTEGER NOT NULL);";
            command.ExecuteNonQuery();

            command.Dispose();
            connection.Close();
            connection.Dispose();

            /*
            // Auslesen des zuletzt eingefügten Datensatzes.
            command.CommandText = "SELECT id, name FROM beispiel ORDER BY id DESC LIMIT 0, 1";

            while (reader.Read())
            {
                Console.WriteLine("Dies ist der {0}. eingefügte Datensatz mit dem Wert: \"{1}\"", reader[0].ToString(), reader[1].ToString());
            }
            */
        }
        #endregion Highscores

        #region Cover
        public static bool GetCover(string CoverPath, ref STexture tex, int MaxSize)
        {
            bool result = false;

            if (!File.Exists(CoverPath))
            {
                CLog.LogError("Can't find File: " + CoverPath);
                return false;
            }

            SQLiteConnection connection = new SQLiteConnection();
            connection.ConnectionString = "Data Source=" + _CoverFilePath;
            SQLiteCommand command;
            try
            {
                connection.Open();
            }
            catch (Exception)
            {
                return false;
            }
            command = new SQLiteCommand(connection);

            command.CommandText = "SELECT id, width, height FROM Cover WHERE [Path] = @path";
            command.Parameters.Add("@path", System.Data.DbType.String, 0).Value = CoverPath;

            SQLiteDataReader reader = null;
            try
            {
                reader = command.ExecuteReader();
            }
            catch (Exception)
            {
                throw;
            }

            if (reader != null && reader.HasRows)
            {
                reader.Read();
                int id = reader.GetInt32(0);
                int w = reader.GetInt32(1);
                int h = reader.GetInt32(2);
                reader.Close();

                command.CommandText = "SELECT Data FROM CoverData WHERE CoverID = " + id.ToString();
                try
                {
                    reader = command.ExecuteReader();
                }
                catch (Exception)
                {
                    throw;
                }

                if (reader.HasRows)
                {
                    result = true;
                    reader.Read();
                    byte[] data = GetBytes(reader);
                    tex = CDraw.AddTexture(w, h, ref data);
                }
            }
            else
            {
                if (reader != null)
                    reader.Close();

                Bitmap origin;
                try
                {
                    origin = new Bitmap(CoverPath);
                }
                catch (Exception)
                {
                    CLog.LogError("Error loading Texture: " + CoverPath);
                    tex = new STexture(-1);

                    if (reader != null)
                    {
                        reader.Close();
                        reader.Dispose();
                    }
                    command.Dispose();
                    connection.Close();
                    connection.Dispose();

                    return false;
                }
                
                int w = MaxSize;
                int h = MaxSize;

                if (origin.Width >= origin.Height && origin.Width > w)
                    h = (int)Math.Round((float)w / origin.Width * origin.Height);
                else if (origin.Height > origin.Width && origin.Height > h)
                    w = (int)Math.Round((float)h / origin.Height * origin.Width);

                Bitmap bmp = new Bitmap(w, h);
                Graphics g = Graphics.FromImage(bmp);
                g.DrawImage(origin, new Rectangle(0, 0, w, h));
                g.Dispose();
                tex = CDraw.AddTexture(bmp);
                byte[] data = new byte[w * h * 4];

                BitmapData bmp_data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                Marshal.Copy(bmp_data.Scan0, data, 0, w*h*4);
                bmp.UnlockBits(bmp_data);
                bmp.Dispose();

                command.CommandText = "INSERT INTO Cover (Path, width, height) " +
                    "VALUES (@path, " + w.ToString() + ", " + h.ToString() + ")";
                command.Parameters.Add("@path", System.Data.DbType.String, 0).Value = CoverPath;
                command.ExecuteNonQuery();

                command.CommandText = "SELECT id FROM Cover WHERE [Path] = @path";
                command.Parameters.Add("@path", System.Data.DbType.String, 0).Value = CoverPath;
                reader = null;
                try
                {
                    reader = command.ExecuteReader();
                }
                catch (Exception)
                {
                    throw;
                }

                if (reader != null)
                {
                    reader.Read();
                    int id = reader.GetInt32(0);
                    reader.Close();
                    command.CommandText = "INSERT INTO CoverData (CoverID, Data) " +
                    "VALUES ('" + id.ToString() + "', @data)";
                    command.Parameters.Add("@data", System.Data.DbType.Binary, 20).Value = data;
                    command.ExecuteReader();
                    result = true;
                }
            }

            if (reader != null)
            {
                reader.Close();
                reader.Dispose();
            }
            command.Dispose();
            connection.Close();
            connection.Dispose();

            return result;
        }

        private static bool InitCoverDB()
        {
            SQLiteConnection connection = new SQLiteConnection();
            connection.ConnectionString = "Data Source=" + _CoverFilePath;
            SQLiteCommand command;

            try
            {
                connection.Open();
            }
            catch (Exception)
            {
                return false;
            }

            command = new SQLiteCommand(connection);
            command.CommandText = "SELECT Value FROM Version";

            SQLiteDataReader reader = null;

            try
            {
                reader = command.ExecuteReader();
            }
            catch (Exception)
            {
                ;
            }

            if (reader == null)
            {
                // create new database/tables
                CreateCoverDB();
            }
            else if (reader.FieldCount == 0)
            {
                // create new database/tables
                CreateCoverDB();
            }
            else
            {
                reader.Read();

                if (reader.GetInt32(0) < CSettings.iDatabaseHighscoreVersion)
                {
                    // update database
                }
            }

            if (reader != null)
            {
                reader.Close();
                reader.Dispose();
            }

            command.Dispose();

            connection.Close();
            connection.Dispose();

            return true;
        }

        private static void CreateCoverDB()
        {
            SQLiteConnection connection = new SQLiteConnection();
            connection.ConnectionString = "Data Source=" + _CoverFilePath;
            SQLiteCommand command;

            try
            {
                connection.Open();
            }
            catch (Exception)
            {
                return;
            }

            command = new SQLiteCommand(connection);

            command.CommandText = "CREATE TABLE IF NOT EXISTS Version ( id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Value INTEGER NOT NULL);";
            command.ExecuteNonQuery();

            command.CommandText = "INSERT INTO Version (id, Value) VALUES(NULL, " + CSettings.iDatabaseCoverVersion.ToString() + ")";
            command.ExecuteNonQuery();

            command.CommandText = "CREATE TABLE IF NOT EXISTS Cover ( id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                "Path TEXT NOT NULL, width INTEGER NOT NULL, height INTEGER NOT NULL);";
            command.ExecuteNonQuery();

            command.CommandText = "CREATE TABLE IF NOT EXISTS CoverData ( id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
                "CoverID INTEGER NOT NULL, Data BLOB NOT NULL);";
            command.ExecuteNonQuery();

            command.Dispose();
            connection.Close();
            connection.Dispose();
        }
        #endregion Cover

        private static byte[] GetBytes(SQLiteDataReader reader)
        {
            const int CHUNK_SIZE = 2 * 1024;
            byte[] buffer = new byte[CHUNK_SIZE];
            long bytesRead;
            long fieldOffset = 0;
            using (MemoryStream stream = new MemoryStream())
            {
                while ((bytesRead = reader.GetBytes(0, fieldOffset, buffer, 0, buffer.Length)) > 0)
                {
                    byte[] actualRead = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, actualRead, 0, (int)bytesRead);
                    stream.Write(actualRead, 0, actualRead.Length);
                    fieldOffset += bytesRead;
                }
                return stream.ToArray();
            }
        }
    }
}
