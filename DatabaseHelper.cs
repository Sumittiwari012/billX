using MySql.Data.MySqlClient;
using System.Collections.Generic;

namespace WpfMySqlCrud
{
    public class Student
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public string Email { get; set; }
    }

    public class DatabaseHelper
    {
        // Set dynamically after MySQLAutoSetup completes
        public static string ConnectionString { get; set; } =
            "Server=localhost;Database=WpfCrudDB;Uid=root;Pwd=;";

        private string _connectionString => ConnectionString;

        // Call once after EnsureMySQLAsync completes
        public void EnsureTableExists()
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var cmd = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS Students (
                    Id    INT AUTO_INCREMENT PRIMARY KEY,
                    Name  VARCHAR(100) NOT NULL,
                    Age   INT          NOT NULL,
                    Email VARCHAR(150) NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;", conn);
            cmd.ExecuteNonQuery();
        }

        // ─── CREATE ───────────────────────────────────────────────
        public bool InsertStudent(Student s)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var cmd = new MySqlCommand(
                "INSERT INTO Students (Name, Age, Email) VALUES (@name, @age, @email)", conn);
            cmd.Parameters.AddWithValue("@name", s.Name);
            cmd.Parameters.AddWithValue("@age", s.Age);
            cmd.Parameters.AddWithValue("@email", s.Email);
            return cmd.ExecuteNonQuery() > 0;
        }

        // ─── READ ─────────────────────────────────────────────────
        public List<Student> GetAllStudents()
        {
            var list = new List<Student>();
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var cmd = new MySqlCommand("SELECT * FROM Students", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Student
                {
                    Id = reader.GetInt32("Id"),
                    Name = reader.GetString("Name"),
                    Age = reader.GetInt32("Age"),
                    Email = reader.GetString("Email")
                });
            }
            return list;
        }

        // ─── UPDATE ───────────────────────────────────────────────
        public bool UpdateStudent(Student s)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var cmd = new MySqlCommand(
                "UPDATE Students SET Name=@name, Age=@age, Email=@email WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@name", s.Name);
            cmd.Parameters.AddWithValue("@age", s.Age);
            cmd.Parameters.AddWithValue("@email", s.Email);
            cmd.Parameters.AddWithValue("@id", s.Id);
            return cmd.ExecuteNonQuery() > 0;
        }

        // ─── DELETE ───────────────────────────────────────────────
        public bool DeleteStudent(int id)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var cmd = new MySqlCommand("DELETE FROM Students WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}