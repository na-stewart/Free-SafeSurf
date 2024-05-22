/*
MIT License

Copyright (c) 2024 Nicholas Aidan Stewart

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace UI
{
    internal class Option
    {
        readonly string[]? availableSelections;
        readonly Action? onSelection;
        string? currentSelection;
        int currentIndex;
        string name;

        public string Name
        {
            get => name;
            set { name = value; }
        }

        internal Option(string name, Action onSelection)
        {
            this.name = name;
            this.onSelection = onSelection;
        }

        internal Option(string name, string[] availableSelections)
        {
            this.name = name;
            this.availableSelections = availableSelections;
            var indexOfConfigValueInAvailableOptions = Array.IndexOf(availableSelections, Config.Instance.Read(name));
            currentIndex = indexOfConfigValueInAvailableOptions > -1 ? indexOfConfigValueInAvailableOptions : 0;
            currentSelection = availableSelections[currentIndex];
        }

        public void Select(ConsoleKeyInfo keyInfo)
        {
            if (availableSelections != null)
            {
                if (keyInfo.Key == ConsoleKey.LeftArrow && currentIndex > 0)
                    currentIndex--;
                else if (keyInfo.Key == ConsoleKey.RightArrow && currentIndex < availableSelections.Length - 1)
                    currentIndex++;
                currentSelection = availableSelections[currentIndex];
            }
            else if (keyInfo.Key == ConsoleKey.Enter)
                onSelection?.Invoke();
        }

        public bool IsExecutable()
        {
            return availableSelections == null;
        }

        public override string? ToString()
        {
            return currentSelection;
        }
    }
}