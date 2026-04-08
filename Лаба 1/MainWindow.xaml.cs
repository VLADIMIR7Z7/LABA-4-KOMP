using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace TextEditor
{
    public partial class MainWindow : Window
    {
        private string currentFilePath = null;
        private bool isTextChanged = false;

        public class SubstringSearchResult
        {
            public string MatchText { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }
            public int Length { get; set; }
            public int AbsoluteIndex { get; set; }
        }

        private class LineInfo
        {
            public string Text { get; set; }
            public int StartIndex { get; set; }
            public int LineNumber { get; set; }
        }

        // Дополнительное задание:
        // автомат для блока 2 (camelCase)
        private enum CamelCaseState
        {
            Start,
            FirstLowercase,
            LettersOrDigits,
            Error
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeNewDocument();
            RegexResultsGrid.ItemsSource = new List<SubstringSearchResult>();
            MatchCountText.Text = "0";
        }

        private void InitializeNewDocument()
        {
            EditorBox.Text = string.Empty;
            EditorBox.Focus();
            UpdateStatusBar();
        }

        // =========================
        // Работа с файлами
        // =========================

        private void CreateFile_Click(object sender, RoutedEventArgs e)
        {
            if (!PromptSaveChanges())
                return;

            EditorBox.Clear();
            currentFilePath = null;
            isTextChanged = false;
            ClearRegexResults();
            UpdateStatusBar();
            StatusText.Text = "Создан новый документ";
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (!PromptSaveChanges())
                return;

            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                Title = "Открыть файл"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    EditorBox.Text = File.ReadAllText(openDialog.FileName);
                    currentFilePath = openDialog.FileName;
                    isTextChanged = false;
                    ClearRegexResults();
                    UpdateStatusBar();
                    StatusText.Text = $"Файл открыт: {Path.GetFileName(currentFilePath)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при открытии файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
                SaveAsFile_Click(sender, e);
            else
                SaveFile(currentFilePath);
        }

        private void SaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                Title = "Сохранить файл как"
            };

            if (saveDialog.ShowDialog() == true)
                SaveFile(saveDialog.FileName);
        }

        private void SaveFile(string filePath)
        {
            try
            {
                File.WriteAllText(filePath, EditorBox.Text);
                currentFilePath = filePath;
                isTextChanged = false;
                UpdateStatusBar();
                StatusText.Text = "Файл сохранён";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (PromptSaveChanges())
                Application.Current.Shutdown();
        }

        private bool PromptSaveChanges()
        {
            if (!isTextChanged)
                return true;

            MessageBoxResult result = MessageBox.Show(
                "Сохранить изменения в файле?",
                "Сохранение",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SaveFile_Click(null, null);
                return true;
            }

            return result != MessageBoxResult.Cancel;
        }

        // =========================
        // Правка
        // =========================

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (EditorBox.CanUndo)
                EditorBox.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (EditorBox.CanRedo)
                EditorBox.Redo();
        }

        private void Cut_Click(object sender, RoutedEventArgs e) => EditorBox.Cut();
        private void Copy_Click(object sender, RoutedEventArgs e) => EditorBox.Copy();
        private void Paste_Click(object sender, RoutedEventArgs e) => EditorBox.Paste();
        private void Delete_Click(object sender, RoutedEventArgs e) => EditorBox.SelectedText = string.Empty;
        private void SelectAll_Click(object sender, RoutedEventArgs e) => EditorBox.SelectAll();

        // =========================
        // ЛР4 — поиск
        // Блок 2: через автомат
        // Блоки 1 и 3: через Regex
        // =========================

        private string GetCurrentPattern()
        {
            switch (SearchTypeComboBox.SelectedIndex)
            {
                case 0:
                    // Блок 1 — логин
                    return @"^[A-Za-z][A-Za-z0-9.-]*$";

                case 1:
                    // Блок 2 — camelCase
                    // Для доп. задания поиск идет через автомат,
                    // это выражение оставлено только для справки
                    return @"^[a-z][a-zA-Z0-9]*$";

                case 2:
                    // Блок 3 — дата
                    return @"^(?:(?:(?:[0-9]{4}-(?:(?:01|03|05|07|08|10|12)-(?:0[1-9]|[12][0-9]|3[01])|(?:04|06|09|11)-(?:0[1-9]|[12][0-9]|30)|02-(?:0[1-9]|1[0-9]|2[0-8]))))|(?:(?:[0-9]{2}(?:0[48]|[2468][048]|[13579][26])|(?:0[48]|[2468][048]|[13579][26])00)-02-29))$";

                default:
                    return string.Empty;
            }
        }

        // Дополнительное задание:
        // автомат для camelCase
        private bool IsCamelCaseByAutomaton(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            CamelCaseState state = CamelCaseState.Start;

            foreach (char ch in input)
            {
                switch (state)
                {
                    case CamelCaseState.Start:
                        if (ch >= 'a' && ch <= 'z')
                            state = CamelCaseState.FirstLowercase;
                        else
                            state = CamelCaseState.Error;
                        break;

                    case CamelCaseState.FirstLowercase:
                    case CamelCaseState.LettersOrDigits:
                        if ((ch >= 'a' && ch <= 'z') ||
                            (ch >= 'A' && ch <= 'Z') ||
                            (ch >= '0' && ch <= '9'))
                        {
                            state = CamelCaseState.LettersOrDigits;
                        }
                        else
                        {
                            state = CamelCaseState.Error;
                        }
                        break;

                    case CamelCaseState.Error:
                        return false;
                }

                if (state == CamelCaseState.Error)
                    return false;
            }

            return state == CamelCaseState.FirstLowercase || state == CamelCaseState.LettersOrDigits;
        }

        private List<LineInfo> GetLinesWithPositions(string text)
        {
            var lines = new List<LineInfo>();
            int lineNumber = 1;
            int start = 0;

            for (int i = 0; i <= text.Length; i++)
            {
                bool endOfLine = i == text.Length || text[i] == '\n';

                if (!endOfLine)
                    continue;

                int length = i - start;
                string lineText = text.Substring(start, length);

                if (lineText.EndsWith("\r"))
                    lineText = lineText.Substring(0, lineText.Length - 1);

                lines.Add(new LineInfo
                {
                    Text = lineText,
                    StartIndex = start,
                    LineNumber = lineNumber
                });

                lineNumber++;
                start = i + 1;
            }

            return lines;
        }

        private List<SubstringSearchResult> FindRegexMatches(string text)
        {
            var results = new List<SubstringSearchResult>();

            foreach (LineInfo line in GetLinesWithPositions(text))
            {
                bool isMatch = false;

                // Блок 2 — через автомат
                if (SearchTypeComboBox.SelectedIndex == 1)
                {
                    isMatch = IsCamelCaseByAutomaton(line.Text);
                }
                else
                {
                    // Блоки 1 и 3 — через Regex
                    Regex regex = new Regex(GetCurrentPattern());
                    Match match = regex.Match(line.Text);
                    isMatch = match.Success && match.Value == line.Text;
                }

                if (isMatch)
                {
                    results.Add(new SubstringSearchResult
                    {
                        MatchText = line.Text,
                        Line = line.LineNumber,
                        Column = 1,
                        Length = line.Text.Length,
                        AbsoluteIndex = line.StartIndex
                    });
                }
            }

            return results;
        }

        private void StartRegexSearch_Click(object sender, RoutedEventArgs e)
        {
            string text = EditorBox.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                ClearRegexResults();
                StatusText.Text = "Нет данных для поиска";
                MessageBox.Show("Нет данных для поиска", "ЛР4", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                List<SubstringSearchResult> results = FindRegexMatches(text);
                RegexResultsGrid.ItemsSource = null;
                RegexResultsGrid.ItemsSource = results;
                MatchCountText.Text = results.Count.ToString();

                if (results.Count == 0)
                {
                    StatusText.Text = "Совпадения не найдены";
                }
                else
                {
                    if (SearchTypeComboBox.SelectedIndex == 1)
                        StatusText.Text = $"Найдено совпадений: {results.Count} (блок 2 — автомат)";
                    else
                        StatusText.Text = $"Найдено совпадений: {results.Count}";

                    RegexResultsGrid.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка поиска";
            }
        }

        private void ClearRegexResults_Click(object sender, RoutedEventArgs e)
        {
            ClearRegexResults();
            EditorBox.Select(0, 0);
            EditorBox.Focus();
            StatusText.Text = "Результаты очищены";
        }

        private void ClearRegexResults()
        {
            RegexResultsGrid.ItemsSource = null;
            RegexResultsGrid.ItemsSource = new List<SubstringSearchResult>();
            MatchCountText.Text = "0";
        }

        private void RegexResultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RegexResultsGrid.SelectedItem is SubstringSearchResult item)
            {
                EditorBox.Focus();
                EditorBox.Select(item.AbsoluteIndex, item.Length);
                EditorBox.SelectionBrush = System.Windows.Media.Brushes.Yellow;
                EditorBox.ScrollToLine(Math.Max(0, item.Line - 1));
                StatusText.Text = $"Выделена подстрока: строка {item.Line}, символ {item.Column}";
            }
        }

        // =========================
        // Служебные окна
        // =========================

        private void RegexInfo_Click(object sender, RoutedEventArgs e)
        {
            ShowInfoWindow(
                "Регулярные выражения и автомат",
                "I блок. Логин\n" +
                @"^[A-Za-z][A-Za-z0-9.-]*$" + "\n\n" +
                "Описание:\n" +
                "- первый символ: латинская буква;\n" +
                "- далее допускаются латинские буквы, цифры, точка и дефис;\n" +
                "- логин не может начинаться с цифры.\n\n" +

                "II блок. Переменная в стиле camelCase\n" +
                @"^[a-z][a-zA-Z0-9]*$" + "\n\n" +
                "Описание:\n" +
                "- начинается со строчной буквы;\n" +
                "- затем идут буквы и цифры.\n" +
                "- для дополнительного задания реализован поиск через граф автомата.\n\n" +

                "III блок. Дата YYYY-MM-DD с учётом високосных годов\n" +
                "Используется полное регулярное выражение из вашего варианта.");
        }

        private void RegexExamples_Click(object sender, RoutedEventArgs e)
        {
            ShowInfoWindow(
                "Тестовые данные",
                "my-login\n" +
                "alpha.beta\n" +
                "test-user\n" +
                "user1\n" +
                "test-2\n\n" +
                "bad_login\n" +
                "123login\n" +
                "-login\n\n" +
                "camelCase\n" +
                "simpleVar2\n" +
                "x\n" +
                "badCamelCase\n" +
                "CamelCase\n\n" +
                "2024-02-29\n" +
                "2023-02-28\n" +
                "2025-12-31\n\n" +
                "2023-02-29\n" +
                "1900-02-29\n" +
                "2024-13-01\n" +
                "2024-00-10");
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            ShowInfoWindow(
                "Справка",
                "Ctrl+N — создать файл\n" +
                "Ctrl+O — открыть файл\n" +
                "Ctrl+S — сохранить файл\n" +
                "F6 — запустить поиск\n\n" +
                "Порядок работы:\n" +
                "1. Введите или откройте текст.\n" +
                "2. Выберите тип поиска.\n" +
                "3. Нажмите «Пуск» или F6.\n" +
                "4. Выберите строку в таблице результатов.\n" +
                "5. Найденная подстрока будет выделена в тексте.\n\n" +
                "Доп. задание:\n" +
                "Для блока 2 поиск реализован через конечный автомат.");
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            ShowInfoWindow(
                "О программе",
                "Текстовый редактор для лабораторной работы 4.\n\n" +
                "Реализован поиск подстрок:\n" +
                "- блок 1: логин (Regex);\n" +
                "- блок 2: camelCase (автомат);\n" +
                "- блок 3: дата YYYY-MM-DD (Regex).\n\n" +
                "Сканер и парсер полностью удалены.");
        }

        private void ShowInfoWindow(string title, string content)
        {
            Window infoWindow = new Window
            {
                Title = title,
                Width = 700,
                Height = 500,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBox
                {
                    Text = content,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(10),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas")
                }
            };

            infoWindow.ShowDialog();
        }

        // =========================
        // Строка состояния
        // =========================

        private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            isTextChanged = true;
            UpdateStatusBar();
        }

        private void EditorBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            try
            {
                int caretIndex = EditorBox.CaretIndex;
                int line = EditorBox.GetLineIndexFromCharacterIndex(caretIndex) + 1;
                int lineStart = EditorBox.GetCharacterIndexFromLineIndex(line - 1);
                int column = caretIndex - lineStart + 1;

                CursorPositionText.Text = $"Стр: {line}, Стб: {column}";
            }
            catch
            {
                CursorPositionText.Text = "Стр: 1, Стб: 1";
            }

            string fileName = string.IsNullOrEmpty(currentFilePath)
                ? "Новый документ"
                : Path.GetFileName(currentFilePath);

            FileInfoText.Text = isTextChanged ? fileName + "*" : fileName;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!PromptSaveChanges())
                e.Cancel = true;

            base.OnClosing(e);
        }
    }
}