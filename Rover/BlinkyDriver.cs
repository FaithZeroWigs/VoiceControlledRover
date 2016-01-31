using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.System.Threading;

namespace Rover
{
    public class BlinkyDriver
    {
        private int _pinNumber;
        private GpioPin _pin;
        private bool _ledOn = false;
        private ThreadPoolTimer _timer;

        public BlinkyDriver(int gpioPinNumber)
        {
            _pinNumber = gpioPinNumber;
            InitGPIO();
        }

        public void Start(int timeIntervalMs)
        {
            _ledOn = true;
            LedLight(true);
            _timer = ThreadPoolTimer.CreatePeriodicTimer(
                                                Timer_Tick, 
                                                TimeSpan.FromMilliseconds(timeIntervalMs));

        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Cancel();
                _timer = null;
            }
            LedLight(false);
        }

        private void InitGPIO()
        {
            _pin = GpioController.GetDefault().OpenPin(_pinNumber);

            _pin.Write(GpioPinValue.High);
            _pin.SetDriveMode(GpioPinDriveMode.Output);
        }

        private void Timer_Tick(ThreadPoolTimer timer)
        {
            _ledOn = !_ledOn;
            LedLight(_ledOn);
        }

        private void LedLight(bool on)
        {
            // The led is connected to 3.3V so it will ligt at low. Revert otherwise.
            GpioPinValue value = on ? GpioPinValue.Low : GpioPinValue.High;
            _pin.Write(value);
        }

    }
}
