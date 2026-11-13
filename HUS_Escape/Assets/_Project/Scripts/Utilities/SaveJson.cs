using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HUSEscape.Persistence
{
    /// <summary>Checks V1 JSON shape before JsonUtility can silently default missing fields.</summary>
    internal static class SaveJson
    {
        internal static T Read<T>(string json, bool legacyCampus = false)
        {
            var parser = new Parser(json);
            object value = parser.Value(0); parser.End();
            Check(value, typeof(T), legacyCampus);
            return JsonUtility.FromJson<T>(json);
        }

        private static void Check(object value, Type type, bool legacyCampus)
        {
            if (type == typeof(string)) { if (!(value is string)) throw Invalid(); return; }
            if (type == typeof(bool)) { if (!(value is bool)) throw Invalid(); return; }
            if (type == typeof(int) || type == typeof(float))
            {
                if (!(value is double number) || double.IsNaN(number) || double.IsInfinity(number) ||
                    (type == typeof(int) ? number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue : Math.Abs(number) > float.MaxValue)) throw Invalid();
                return;
            }
            if (type.IsArray)
            {
                if (!(value is List<object> list)) throw Invalid();
                foreach (object item in list) Check(item, type.GetElementType(), false);
                return;
            }
            if (!(value is Dictionary<string, object> fields)) throw Invalid();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                // UTC is informational in Floor V1. Campus V1 predates Office facts.
                bool optional = field.Name == "savedUtc" || (legacyCampus && field.Name == "officeFacts");
                if (!fields.TryGetValue(field.Name, out var child)) { if (optional) continue; throw new InvalidDataException("Save thiếu trường: " + field.Name); }
                if (optional && child == null) continue;
                Check(child, field.FieldType, false);
            }
        }
        private static InvalidDataException Invalid() => new InvalidDataException("JSON save hỏng hoặc sai kiểu dữ liệu.");

        private sealed class Parser
        {
            private readonly string text;
            private int offset;
            internal Parser(string json) { text = json ?? throw Invalid(); }
            private void Space() { while (offset < text.Length && (text[offset] == ' ' || text[offset] == '\t' || text[offset] == '\r' || text[offset] == '\n')) offset++; }
            internal void End() { Space(); if (offset != text.Length) throw Invalid(); }
            private bool Take(char c) { Space(); if (offset >= text.Length || text[offset] != c) return false; offset++; return true; }
            private void Need(char c) { if (!Take(c)) throw Invalid(); }
            internal object Value(int depth)
            {
                if (depth > 32) throw Invalid(); Space(); if (offset >= text.Length) throw Invalid();
                char c = text[offset];
                if (c == '{')
                {
                    offset++; var fields = new Dictionary<string, object>(StringComparer.Ordinal);
                    if (Take('}')) return fields;
                    do { string key = String(); Need(':'); if (fields.ContainsKey(key)) throw Invalid(); fields.Add(key, Value(depth + 1)); } while (Take(','));
                    Need('}'); return fields;
                }
                if (c == '[')
                {
                    offset++; var list = new List<object>(); if (Take(']')) return list;
                    do { list.Add(Value(depth + 1)); } while (Take(',')); Need(']'); return list;
                }
                if (c == '"') return String();
                foreach (string literal in new[] { "true", "false", "null" })
                    if (offset + literal.Length <= text.Length && string.CompareOrdinal(text, offset, literal, 0, literal.Length) == 0)
                    { offset += literal.Length; return literal == "null" ? null : (object)(literal == "true"); }
                int start = offset; if (text[offset] == '-') offset++;
                Digits();
                if (offset < text.Length && text[offset] == '.') { offset++; Digits(); }
                if (offset < text.Length && (text[offset] == 'e' || text[offset] == 'E'))
                { offset++; if (offset < text.Length && (text[offset] == '+' || text[offset] == '-')) offset++; Digits(); }
                string number = text.Substring(start, offset - start);
                string unsigned = number.TrimStart('-');
                if (unsigned.Length > 1 && unsigned[0] == '0' && char.IsDigit(unsigned[1])) throw Invalid();
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)) throw Invalid();
                return result;
            }
            private void Digits() { int start = offset; while (offset < text.Length && text[offset] >= '0' && text[offset] <= '9') offset++; if (start == offset) throw Invalid(); }
            private string String()
            {
                Need('"'); var result = new StringBuilder();
                while (offset < text.Length)
                {
                    char c = text[offset++]; if (c == '"') return result.ToString();
                    if (c < 32) throw Invalid();
                    if (c != '\\') { result.Append(c); continue; }
                    if (offset >= text.Length) throw Invalid(); c = text[offset++];
                    switch (c)
                    {
                        case '"': case '\\': case '/': result.Append(c); break;
                        case 'b': result.Append('\b'); break; case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break; case 'r': result.Append('\r'); break; case 't': result.Append('\t'); break;
                        case 'u':
                            if (offset + 4 > text.Length || !ushort.TryParse(text.Substring(offset, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code)) throw Invalid();
                            offset += 4; result.Append((char)code); break;
                        default: throw Invalid();
                    }
                }
                throw Invalid();
            }
        }
    }
}
