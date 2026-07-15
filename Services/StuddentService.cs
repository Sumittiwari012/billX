using MySql.Data.MySqlClient;
using MyWPFCRUDApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWPFCRUDApp.Services
{
    public class StudentService

    {
        private string Con => DatabaseHelper.ConnectionString;
        // ─── CREATE ───────────────────────────────────────────────

        public bool InsertStudent(Student s)
        {
            using var conn = new MySqlConnection(Con);
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
            using var conn = new MySqlConnection(Con);
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
            using var conn = new MySqlConnection(Con);
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
            using var conn = new MySqlConnection(Con);
            conn.Open();
            var cmd = new MySqlCommand("DELETE FROM Students WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
