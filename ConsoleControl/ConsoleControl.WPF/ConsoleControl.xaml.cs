using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ProcessInterface;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using System.Collections.Generic;
using System.Diagnostics;

namespace ConsoleControl.WPF
{
    /// <summary>
    /// Interaction logic for ConsoleControl.xaml
    /// </summary>
    public partial class ConsoleControl : UserControl
    {
        public ConsoleControl()
        {
            InitializeComponent();
            //  Handle process events.
            processInterface.OnProcessOutput += new ProcessInterface.ProcessEventHanlder(processInterace_OnProcessOutput);
            processInterface.OnProcessError += processInterace_OnProcessError;
            processInterface.OnProcessInput += new ProcessInterface.ProcessEventHanlder(processInterace_OnProcessInput);
            processInterface.OnProcessExit += new ProcessInterface.ProcessEventHanlder(processInterace_OnProcessExit);

            //  Wait for key down messages on the rich text box.
            richTextBoxConsole.KeyDown += new KeyEventHandler(richTextBoxConsole_KeyDown);
            richTextBoxConsole.PreviewKeyDown += new KeyEventHandler(richTextBoxConsole_KeyDownInput);
            richTextBoxConsole.PreviewTextInput += richTextBoxConsole_PreviewTextInput;
            richTextBoxConsole.IsReadOnly = true;
            richTextBoxConsole.IsReadOnlyCaretVisible = true;
        }


        /// <summary>
        /// Handles the OnProcessError event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        void processInterace_OnProcessError(object sender, ProcessInterface.ProcessEventArgs args)
        {
            //  Write the output, in red
            WriteOutput(args.Content, Colors.Red);

            //  Fire the output event.
            FireProcessOutputEvent(args);
        }

        /// <summary>
        /// Handles the OnProcessOutput event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        void processInterace_OnProcessOutput(object sender, ProcessInterface.ProcessEventArgs args)
        {
            //  Write the output, in white
            WriteOutput(args.Content, Colors.White);

            //  Fire the output event.
            FireProcessOutputEvent(args);
        }

        /// <summary>
        /// Handles the OnProcessInput event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessInterface.ProcessEventArgs"/> instance containing the event data.</param>
        void processInterace_OnProcessInput(object sender, ProcessInterface.ProcessEventArgs args)
        {
            FireProcessInputEvent(args);
        }

        /// <summary>
        /// Handles the OnProcessExit event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessInterface.ProcessEventArgs"/> instance containing the event data.</param>
        void processInterace_OnProcessExit(object sender, ProcessInterface.ProcessEventArgs args)
        {
            //  Are we showing diagnostics?
            if (ShowDiagnostics)
            {
                WriteOutput(System.Environment.NewLine + processInterface.ProcessFileName + " exited.", Color.FromArgb(255, 0, 255, 0));
            }

            //  Read only again.
            RunOnUIDespatcher((Action)(() =>
            {
                richTextBoxConsole.IsReadOnly = true;
            }));

            //  And we're no longer running.
            IsProcessRunning = false;
        }

        /// <summary>
        /// Handles the KeyDown event of the richTextBoxConsole control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.KeyEventArgs"/> instance containing the event data.</param>
        ///
        void richTextBoxConsole_KeyDown(object sender, KeyEventArgs e)
        {
            richTextBoxConsole.IsReadOnly = true;
            bool inReadOnlyZone = richTextBoxConsole.Selection.Start.CompareTo(inputStart) < 0;

            //  If we're at the input point and it's backspace, bail.
            if (inReadOnlyZone && e.Key == Key.Back)
                e.Handled = true;

            //  Are we in the read-only zone?
            if (inReadOnlyZone)
            {
                //  Allow arrows and Ctrl-C.
                if (!(e.Key == Key.Left ||
                    e.Key == Key.Right ||
                    e.Key == Key.Up ||
                    e.Key == Key.Down ||
                    (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))))
                {
                    e.Handled = true;
                }
            }
            //  Is it the return key?
            if (e.Key == Key.Return)
            {
                richTextBoxConsole.IsReadOnly = true;
                //  Get the input.
                //todo
                //string input = new TextRange(inputStart, (richTextBoxConsole.Selection.Start.) - inputStart);

                //  Write the input (without echoing).
                //todoWriteInput(input, Colors.White, false);
            }
        }
        void richTextBoxConsole_KeyDownInput(object sender, KeyEventArgs e)
        {
            richTextBoxConsole.IsReadOnly = true;
            
            if (e.Key == Key.Space)
            {
                inputCommand += " ";
                WriteOutput(" ", Colors.White);
            }
            else if (e.Key == Key.Back)
            {
                richTextBoxConsole.IsReadOnly = false;
                inputCommand.Substring(0,inputCommand.Length-1);
            }
        }
        public bool inputBegin = false;
        public string inputCommand = "";
        private void richTextBoxConsole_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            richTextBoxConsole.IsReadOnly = true;
            if(e.Text.Equals("\r"))
            {
                WriteInput(inputCommand, Colors.White, false);
                inputCommand = "";
                inputBegin = false;
            }
            else
            {
                if(inputBegin == false)
                {
                    inputBegin = true;
                    inputCommand = "";
                }
                inputCommand += e.Text;
                WriteOutput(e.Text, Colors.White);

            }
        }

        /// <summary>
        /// Writes the output to the console control.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="color">The color.</param>
        public void WriteOutput(string output, Color color)
        {
            richTextBoxConsole.IsReadOnly = true;
            if (string.IsNullOrEmpty(lastInput) == false &&
                (output == lastInput || output.Replace("\r\n", "") == lastInput))
                return;

            RunOnUIDespatcher((Action)(() =>
            {
                //  Write the output.
                richTextBoxConsole.Selection.ApplyPropertyValue(TextBlock.ForegroundProperty, new SolidColorBrush(color));
                richTextBoxConsole.AppendText(output);
                triggerLogChanged(output);
                richTextBoxConsole.CaretPosition = richTextBoxConsole.Document.ContentEnd;
                if (scrollBarAtBottom())
                {
                    richTextBoxConsole.ScrollToEnd();
                }
                inputStart = richTextBoxConsole.Selection.Start;
            }));
        }

        public delegate void LogChangedEventHandler(string output);

        public event LogChangedEventHandler LogChangedEvent;

        public void triggerLogChanged(string output)
        {
            // Your logic
            if (LogChangedEvent != null)
            {
                LogChangedEvent(output);
            }
        }
        //SCROLL BAR STUFF
        public bool scrollBarAtBottom()
        { 
            // get the vertical scroll position
            double dVer = richTextBoxConsole.VerticalOffset;
 
            //get the vertical size of the scrollable content area
            double dViewport = richTextBoxConsole.ViewportHeight;
 
            //get the vertical size of the visible content area
            double dExtent = richTextBoxConsole.ExtentHeight;
 
            if (dVer != 0)
            {
                if (dVer + dViewport == dExtent)
                    return true;
                else
                    return false;
            }
            else if(dExtent < dViewport)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void ClearOutput()
        {
           //todo richTextBoxConsole.Clear();
            inputStart = null;
        }
    ///////////////////////////

        /// <summary>
        /// Writes the input to the console control.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="color">The color.</param>
        /// <param name="echo">if set to <c>true</c> echo the input.</param>
        public void WriteInput(string input, Color color, bool echo)
        {
            RunOnUIDespatcher((Action)(() =>
            {
                //  Are we echoing?
                if (echo)
                {
                    richTextBoxConsole.Selection.ApplyPropertyValue(TextBlock.ForegroundProperty, new SolidColorBrush(color));
                    richTextBoxConsole.AppendText(input);
                    richTextBoxConsole.CaretPosition = richTextBoxConsole.Document.ContentEnd;
                    inputStart = richTextBoxConsole.Selection.Start;
                }

                lastInput = input;

                //  Write the input.
                processInterface.WriteInput(input);

                //  Fire the event.
                FireProcessInputEvent(new ProcessEventArgs(input));
            }));
        }

        private void RunOnUIDespatcher(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                //  Invoke the action.
                action();
            }
            else
            {
                Dispatcher.BeginInvoke(action, null);
            }
        }


        /// <summary>
        /// Runs a process.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="arguments">The arguments.</param>
        public void StartProcess(string fileName, string arguments)
        {
            //  Are we showing diagnostics?
            if (ShowDiagnostics)
            {
                WriteOutput("Preparing to run " + fileName, Color.FromArgb(255, 0, 255, 0));
                if (!string.IsNullOrEmpty(arguments))
                    WriteOutput(" with arguments " + arguments + "." + Environment.NewLine, Color.FromArgb(255, 0, 255, 0));
                else
                    WriteOutput("." + Environment.NewLine, Color.FromArgb(255, 0, 255, 0));
            }

            //  Start the process.
            processInterface.StartProcess(fileName, arguments);

            //  If we enable input, make the control not read only.
            if (IsInputEnabled)
                richTextBoxConsole.IsReadOnly = true;

            //  We're now running.
            IsProcessRunning = true;
        }

        /// <summary>
        /// Stops the process.
        /// </summary>
        public void StopProcess()
        {
            //  Stop the interface.
            processInterface.StopProcess();
        }

        /// <summary>
        /// Fires the console output event.
        /// </summary>
        /// <param name="args">The <see cref="ProcessInterface.ProcessEventArgs"/> instance containing the event data.</param>
        private void FireProcessOutputEvent(ProcessEventArgs args)
        {
            //  Get the event.
            var theEvent = OnProcessOutput;
            if (theEvent != null)
                theEvent(this, args);
        }

        /// <summary>
        /// Fires the console input event.
        /// </summary>
        /// <param name="args">The <see cref="ProcessInterface.ProcessEventArgs"/> instance containing the event data.</param>
        private void FireProcessInputEvent(ProcessEventArgs args)
        {
            //  Get the event.
            var theEvent = OnProcessInput;
            if (theEvent != null)
                theEvent(this, args);
        }

        /// <summary>
        /// The internal process interface used to interface with the process.
        /// </summary>
        private ProcessInterface.ProcessInterface processInterface = new ProcessInterface.ProcessInterface();

        /// <summary>
        /// Current position that input starts at.
        /// </summary>
        private TextPointer inputStart;
        
        /// <summary>
        /// The last input string (used so that we can make sure we don't echo input twice).
        /// </summary>
        private string lastInput;
        
        /// <summary>
        /// Occurs when console output is produced.
        /// </summary>
        public event ProcessEventHanlder OnProcessOutput;

        /// <summary>
        /// Occurs when console input is produced.
        /// </summary>
        public event ProcessEventHanlder OnProcessInput;
          
        private static readonly DependencyProperty ShowDiagnosticsProperty = 
          DependencyProperty.Register("ShowDiagnostics", typeof(bool), typeof(ConsoleControl),
          new PropertyMetadata(false, new PropertyChangedCallback(OnShowDiagnosticsChanged)));
        
        public bool ShowDiagnostics
        {
            get { try { return (bool)GetValue(ShowDiagnosticsProperty); } catch { return false; } }//$$$ added try catch for stop process crash
          set { SetValue(ShowDiagnosticsProperty, value); }
        }
        
        public Process getProcess()
        {
            return processInterface.Process;
        }

        private static void OnShowDiagnosticsChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
        {
          ConsoleControl me = o as ConsoleControl;
        }
        
        
        private static readonly DependencyProperty IsInputEnabledProperty = 
          DependencyProperty.Register("IsInputEnabled", typeof(bool), typeof(ConsoleControl),
          new PropertyMetadata(true, new PropertyChangedCallback(OnIsInputEnabledChanged)));
        
        public bool IsInputEnabled
        {
          get { return (bool)GetValue(IsInputEnabledProperty); }
          set { SetValue(IsInputEnabledProperty, value); }
        }
        
        private static void OnIsInputEnabledChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
        {
          ConsoleControl me = o as ConsoleControl;
        }

        
        internal static readonly DependencyPropertyKey IsProcessRunningPropertyKey =
          DependencyProperty.RegisterReadOnly("IsProcessRunning", typeof(bool), typeof(ConsoleControl),
          new PropertyMetadata(false));

        private static readonly DependencyProperty IsProcessRunningProperty = IsProcessRunningPropertyKey.DependencyProperty;

        public bool IsProcessRunning
        {
            get { return (bool)GetValue(IsProcessRunningProperty); }
            private set { try { SetValue(IsProcessRunningPropertyKey, value); } catch {  } } //$$$ added try catch for stop process crash
        }

        private static void OnIsProcessRunningChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
        {
            ConsoleControl me = o as ConsoleControl;
        }
                
    }
}
