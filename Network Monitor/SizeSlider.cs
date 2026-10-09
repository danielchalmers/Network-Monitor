using System;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Network_Monitor;

/// <summary>
/// The Size slider, which moves along the same logarithmic scale as Ctrl+scroll.
/// Screen readers are given the widget's actual size rather than the position on that scale.
/// </summary>
public class SizeSlider : Slider
{
    public SizeSlider()
    {
        Minimum = SizeScaleConverter.MinSizeLog;
        Maximum = SizeScaleConverter.MaxSizeLog;
        SmallChange = SizeScaleConverter.StepSize;
        LargeChange = SizeScaleConverter.StepSize * 5;

        // The scale is too short for clicks on the track to page along it, so a click goes straight to that size.
        IsMoveToPointEnabled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SizeSliderAutomationPeer(this);

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);

        if (UIElementAutomationPeer.FromElement(this) is SizeSliderAutomationPeer peer)
        {
            peer.RaisePropertyChangedEvent(
                RangeValuePatternIdentifiers.ValueProperty,
                (double)SizeScaleConverter.FromLogSize(oldValue),
                (double)SizeScaleConverter.FromLogSize(newValue));
        }
    }

    /// <summary>
    /// Reports the slider as going from the smallest size to the largest, in the same units as the Size setting.
    /// </summary>
    private sealed class SizeSliderAutomationPeer : FrameworkElementAutomationPeer, IRangeValueProvider
    {
        public SizeSliderAutomationPeer(SizeSlider owner)
            : base(owner)
        {
        }

        private SizeSlider Slider => (SizeSlider)Owner;

        public double Value => SizeScaleConverter.FromLogSize(Slider.Value);

        public double Minimum => SizeScaleConverter.MinSize;

        public double Maximum => SizeScaleConverter.MaxSize;

        public double SmallChange => SizeScaleConverter.ScaleSize((int)Value, 1) - Value;

        public double LargeChange => SizeScaleConverter.ScaleSize((int)Value, 5) - Value;

        public bool IsReadOnly => !IsEnabled();

        public void SetValue(double value)
        {
            if (!IsEnabled())
                throw new ElementNotEnabledException();

            if (value < Minimum || value > Maximum)
                throw new ArgumentOutOfRangeException(nameof(value));

            Slider.Value = SizeScaleConverter.ToLogSize(value);
        }

        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string GetClassNameCore() => nameof(System.Windows.Controls.Slider);
    }
}
