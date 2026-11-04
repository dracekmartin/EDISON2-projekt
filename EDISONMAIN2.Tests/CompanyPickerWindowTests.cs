using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.CompanyPickerWindow;
using EDTesting;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

/// <summary>Okno jen binduje VM; překlep v cestě by WPF tiše spolkl a výběr firmy by zůstal prázdný.</summary>
public class CompanyPickerWindowTests
{
    private static CompanyPickerViewModel Vm() => new(
        new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()),
        new CompanyStruct { Id = "DIN", Name = "Dinarica" },
        [new CompanyStruct { Id = "JI_DP", Name = "DP Jihlava" }, new CompanyStruct { Id = "PI_DP", Name = "DP Písek" }]);

    [Fact]
    public void Window_BindsMessageAndPreselectedCompany()
    {
        Sta.RunOnAppThread(() =>
        {
            SetupWindowTests.EnsureApplication();
            var window = new CompanyPickerWindow(Vm());
            try
            {
                Assert.Empty(SetupWindowTests.BindingsOf(window)
                                             .Where(b => b.Status == BindingStatus.PathError)
                                             .Select(b => b.ParentBinding.Path.Path));
                Assert.Equal("JI_DP", window.CompanyCombo.SelectedKey);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [WindowFact]
    public void EnterInClosedCompanyField_ConfirmsDialog()
    {
        // Striktní pole Enter spotřebuje; bez obsluhy okna by šel dialog potvrdit jen myší a Esc by ukončil aplikaci.
        Sta.RunOnAppThread(() =>
        {
            SetupWindowTests.EnsureApplication();
            var vm = Vm();
            var window = new CompanyPickerWindow(vm)
            {
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -3000, Top = 0,
                ShowInTaskbar = false, ShowActivated = false,
            };
            bool? result = null;
            vm.RequestClose += r => result = r;
            window.Show();
            try
            {
                PressEnter(window.CompanyCombo);

                Assert.True(result);
                Assert.Equal("JI_DP", vm.Picked?.Id);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [WindowFact]
    public void EnterInOpenCompanyList_OnlyClosesList()
    {
        // Enter v rozbaleném seznamu potvrzuje položku, dialog zůstává otevřený pro kontrolu.
        Sta.RunOnAppThread(() =>
        {
            SetupWindowTests.EnsureApplication();
            var vm = Vm();
            var window = new CompanyPickerWindow(vm)
            {
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -3000, Top = 0,
                ShowInTaskbar = false, ShowActivated = false,
            };
            bool closed = false;
            vm.RequestClose += _ => closed = true;
            window.Show();
            try
            {
                window.CompanyCombo.IsDropDownOpen = true;

                PressEnter(window.CompanyCombo);

                Assert.False(closed);
                Assert.False(window.CompanyCombo.IsDropDownOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    // Jako InputManager: Preview a bublající KeyDown sdílí args (vzor EDFooterTests.PressEsc).
    private static void PressEnter(UIElement target)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, Key.Enter)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        args.RoutedEvent = Keyboard.KeyDownEvent;
        target.RaiseEvent(args);
    }
}
