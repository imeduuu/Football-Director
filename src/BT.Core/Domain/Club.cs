using System;

namespace BT.Core.Domain;

/// <summary>A football club in the simulated world.</summary>
public sealed class Club
{
    public Club(string id, string name, string nationality)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("A stable club id is required.", nameof(id)) : id;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A club name is required.", nameof(name)) : name;
        Nationality = string.IsNullOrWhiteSpace(nationality) ? throw new ArgumentException("A nationality is required.", nameof(nationality)) : nationality;
    }
    public string Id { get; }
    public string Name { get; }
    public string Nationality { get; }
}
