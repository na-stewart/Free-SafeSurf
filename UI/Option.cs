namespace UI
{
    internal class Option
    {
        private string selection;
        private string name;
        private string[] availableSelections;
        private int selectionIndex;
        private Action onSelection;


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
            selectionIndex = indexOfConfigValueInAvailableOptions > -1 ? indexOfConfigValueInAvailableOptions : 0;
            selection = availableSelections[selectionIndex];
        }

        public void Select(ConsoleKeyInfo keyInfo)
        {
            if (availableSelections != null)
            {
                if (keyInfo.Key == ConsoleKey.LeftArrow && selectionIndex > 0)
                    selectionIndex--;
                else if (keyInfo.Key == ConsoleKey.RightArrow && selectionIndex < availableSelections.Length - 1)
                    selectionIndex++;
                selection = availableSelections[selectionIndex];
            }
            else if (keyInfo.Key == ConsoleKey.Enter)
                onSelection();
        }

        public bool isExecutable()
        {
            return availableSelections == null;
        }

        public override string ToString()
        {
            return selection;
        }
    }
}