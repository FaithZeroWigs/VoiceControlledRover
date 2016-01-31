using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rover
{
    public class TwoMotorsDriver
    {
        private readonly Motor _leftMotor;
        private readonly Motor _rightMotor;

        public TwoMotorsDriver(Motor leftMotor, Motor rightMotor)
        {
            _leftMotor = leftMotor;
            _rightMotor = rightMotor;
        }

        public void Stop()
        {
            _leftMotor.Stop();
            _rightMotor.Stop();
        }

        public void MoveForward()
        {
            _leftMotor.MoveForward();
            _rightMotor.MoveForward();
        }

        public void MoveBackward()
        {
            _leftMotor.MoveBackward();
            _rightMotor.MoveBackward();
        }

        public async Task TurnRightAsync()
        {
            _leftMotor.MoveForward();
            _rightMotor.MoveBackward();

            await Task.Delay(TimeSpan.FromMilliseconds(200));

            Stop();
        }

        public async Task TurnLeftAsync()
        {
            _leftMotor.MoveBackward();
            _rightMotor.MoveForward();

            await Task.Delay(TimeSpan.FromMilliseconds(200));

            Stop();
        }

        public async Task TurnAroundAsync()
        {
            _leftMotor.MoveBackward();
            _rightMotor.MoveForward();

            await Task.Delay(TimeSpan.FromMilliseconds(1200));

            Stop();
        }

        public async Task PlayAsync()
        {
            await TurnLeftAsync();
            await TurnRightAsync();
            await TurnLeftAsync();
            await TurnRightAsync();
            await TurnAroundAsync();
        }

        public async Task AttackAsync()
        {
            MoveForward();
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            Stop();
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            MoveForward();
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            Stop();
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            MoveBackward();
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            Stop();
        }


    }
}