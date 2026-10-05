using System.Collections.Generic;
using System.Linq;
using ContextManagement;
using TMPro;
using UnityEngine;

public class Book : MonoBehaviour, IEnumProvider
{
    public string bookName, genre;

    public static Dictionary<string, Book> books = new();
    private void Start()
    {
        GetComponentInChildren<TMP_Text>().text = '\"' + bookName + '\"';
        books.Add(ToString(), this);
        Debug.Log(ToString());
    }

    public override string ToString()
    {
        return $"\"{bookName}\" - {genre}";
    }

    public IEnumerable<string> GetEnums()
    {
        return books.Keys;
    }
}
