using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace P2Trainer
{
    public partial class MainWindow : Window
    {

        globalKeyboardHook kbHook = new globalKeyboardHook();
        System.Windows.Forms.Timer updateTimer;
        Process game;
        public bool hooked = false;

        Thread ScanThread;
        DeepPointer positionDP;
        DeepPointer velocityDP;
        IntPtr positionPtr;
        IntPtr velocityPtr;
        Vector3f velocity = new Vector3f(0, 0, 0);
        Vector3f position = new Vector3f(0, 0, 0);
        Vector3f savedPos = new Vector3f(0, 0, 0);


        public MainWindow()
        {
            InitializeComponent();

            kbHook.KeyDown += InputKeyDown;
            kbHook.KeyUp += InputKeyUp;
            kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F7);
            kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F8);


            updateTimer = new System.Windows.Forms.Timer
            {
                Interval = (16) // ~60 Hz
            };
            updateTimer.Tick += new EventHandler(Update);
            updateTimer.Start();
        }

        private void Update(object sender, EventArgs e)
        {
            if (game == null || game.HasExited)
            {
                game = null;
                hooked = false;
            }
            if (!hooked)
                hooked = Hook();
            if (!hooked)
                return;
            if (ScanThread.IsAlive)
                return;

            try
            {
                DerefPointers();
            }
            catch (Exception)
            {
                return;
            }

            game.ReadValue<Vector3f>(positionPtr, out position);
            game.ReadValue<Vector3f>(velocityPtr, out velocity);

            Vector vel = new Vector(velocity.X, velocity.Y);

            positionBlock.Text = position.X.ToString("0.00") + "\n" + position.Y.ToString("0.00") + "\n" + position.Z.ToString("0.00");
            speedBlock.Text = (vel.Length).ToString("0.00");
            zBlock.Text = (velocity.Z).ToString("0.00");
        }

        private void SavePosBtn_Click(object sender, RoutedEventArgs e)
        {
            SavePosition();
        }

        private void LoadPosBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadPosition();
        }

        private void SavePosition()
        {
            savedPos = position;
        }

        private void LoadPosition()
        {
            game.WriteBytes(positionPtr, BitConverter.GetBytes(savedPos.X));
            game.WriteBytes(positionPtr + 0x4, BitConverter.GetBytes(savedPos.Y));
            game.WriteBytes(positionPtr + 0x8, BitConverter.GetBytes(savedPos.Z));
        }

        private void SetPointersBySigscan()
        {
            
            CancellationTokenSource CancelSource = new CancellationTokenSource();
            ScanThread = new Thread(() =>
            {
                Console.WriteLine("Starting scan thread");

                var gWorldPtr = IntPtr.Zero;
                var gWorldSig = new SigScanTarget(10, "80 7C 24 ?? 00 ?? ?? 48 8B 3D ???????? 48")
                { OnFound = (p, s, ptr) => ptr + 0x4 + p.ReadValue<int>(ptr) };

                var scanner = new SignatureScanner(game, game.MainModule.BaseAddress, (int)game.MainModule.ModuleMemorySize);
                var token = CancelSource.Token;

                while (!token.IsCancellationRequested)
                {
                    gWorldPtr = scanner.Scan(gWorldSig);
                    if (gWorldPtr != IntPtr.Zero)
                    {

                        positionDP = new DeepPointer(gWorldPtr, 0x180, 0x38, 0x0, 0x30, 0x2d0, 0x130, 0x1f0);
                        velocityDP = new DeepPointer(gWorldPtr, 0x180, 0x38, 0x0, 0x30, 0x2d0, 0x130, 0x158);
                        //velocityDP = new DeepPointer(gWorldPtr, 0x180, 0x38, 0x0, 0x30, 0x2d0, 0x2b8, 0x2ac);

                        Console.WriteLine("Found GWorld at 0x" + gWorldPtr.ToString("X") + ".");
                        break;
                    }

                    Thread.Sleep(2000);
                }

                Console.WriteLine("Exiting scan thread.");
            });

            ScanThread.Start();
        }

            private bool Hook()
        {
            List<Process> processList = Process.GetProcesses().ToList().FindAll(x => x.ProcessName.Contains("Psychonauts2-Win"));
            if (processList.Count == 0)
            {
                game = null;
                return false;
            }
            game = processList[0];

            if (game.HasExited)
                return false;

            try
            {
                SetPointersBySigscan();
                return true;
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine(ex.ErrorCode);
                return false;
            }
        }

        private void DerefPointers()
        {
            IntPtr posPtr;
            positionDP.DerefOffsets(game, out posPtr);
            positionPtr = posPtr;

            IntPtr velPtr;
            velocityDP.DerefOffsets(game, out velPtr);
            velocityPtr = velPtr;
        }

        private void InputKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F7:
                    SavePosition();
                    break;
                case Keys.F8:
                    LoadPosition();
                    break;
                default:
                    break;
            }
            e.Handled = true;
        }

        private void InputKeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }
    }
}