using System;
using System.Collections;
using System.Collections.Generic;

namespace ConsoleEngine.Collections;

public class Deque<T> : IEnumerable<T>
{
    private Node _frontNode;
    private Node _backNode;

    public T Front => _frontNode == null ? default : _frontNode.Value;
    public T Back => _backNode == null ? default : _backNode.Value;
        
    public int Count { get; private set; } = 0;        

    public void PushFront(T item)
    {
        // TODO: is it possible to avoid allocation? pooling? :/ 
        var newNode = new Node(item);

        newNode.LinkRight(_frontNode);
            
        _frontNode = newNode;
        _backNode ??= newNode;
        Count++;
    }

    public void PushBack(T item)
    {
        // TODO: is it possible to avoid allocation? pooling? :/
        var newNode = new Node(item);
            
        newNode.LinkLeft(_backNode);
            
        _frontNode ??= newNode;
        _backNode = newNode;
        Count++;
    }

    public T PopFront()
    {
        if (_frontNode == null) throw new InvalidOperationException("Empty deque");

        var toRemove = _frontNode;
        _frontNode = _frontNode.Right;
            
        toRemove.Unlink();
            
        Count--;
        return toRemove.Value;
    }

    public T PopBack()
    {
        if (_backNode == null) throw new InvalidOperationException("Empty deque");
            
        var toRemove = _backNode;
        _backNode = _backNode.Left;
            
        toRemove.Unlink();
            
        Count--;
        return toRemove.Value;
    }

    public IEnumerator<T> GetEnumerator() => new Enumerator(_frontNode);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public class Node(T value)
    {
        public T Value { get; } = value;
        public Node Left { get; private set; }
        public Node Right { get; private set; }


        public void LinkLeft(Node other)
        {
            if (other != null)
                other.Right = this;
            Left = other;
        }

        public void LinkRight(Node other)
        {
            if (other != null)
                other.Left = this;
            Right = other;
        }
            
        public void Unlink()
        {
            UnlinkLeft();
            UnlinkRight();
        }

        public void UnlinkLeft()
        {
            if (Left?.Right != null)
                Left.Right = null;
                
            Left = null;
        }

        public void UnlinkRight()
        {
            if (Right?.Left != null)
                Right.Left = null;
                
            Right = null;
        }
    }

    private struct Enumerator(Node current) : IEnumerator<T>
    {
        private Node _current = null;

        public bool MoveNext()
        {
            if (_current == null) {
                _current = current;
                 
                return _current != null;
            }

            return (_current = _current.Right) != null;
        }

        public void Reset() => _current = null;

        public T Current => _current.Value;

        object IEnumerator.Current => Current;

        public void Dispose() {}
    }
}