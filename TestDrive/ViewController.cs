using System;
using UIKit;
using CoreGraphics;
using System.Threading.Tasks;
using System.Timers;

namespace TestDrive
{
	public partial class ViewController : UIViewController
	{
		private UITextView textView;
		private Timer timer;
		private Obd obd;

		protected ViewController(IntPtr handle) : base(handle)
		{
			// Note: this .ctor should not contain any initialization logic.
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();
			// Perform any additional setup after loading the view, typically from a nib.

			View.BackgroundColor = UIColor.Gray;

			nfloat h = 250.0f;
			nfloat w = View.Bounds.Width;

			textView = new UITextView
			{
				Frame = new CGRect(10, 82, w - 20, h),
				Editable = false,
				Font = UIFont.FromName("Helvetica-Bold", 20f)
			};

			View.AddSubview(textView);

			try
			{
				obd = new Obd();

				textView.Text = "initializing...";

				bool isInitialized = false;

				var initTasks = Task.Run(async () => { isInitialized = await obd.Init(); });

				Task.WaitAll(initTasks);

				if (!isInitialized)
				{
					textView.Text += "\nerror initializing obd";

					return;
				}

				textView.Text += "\ninitialization complete\nreading obd data";

				Task.Delay(2000);
			}
			catch (Exception ex)
			{
				textView.Text = ex.Message;
			}

			if (timer == null)
			{
				timer = new Timer();
				timer.Enabled = true;
				timer.Interval = 500;
				timer.Elapsed += PollObd;
				timer.Start();
			}
		}

		public override void DidReceiveMemoryWarning()
		{
			base.DidReceiveMemoryWarning();
			// Release any cached data, images, etc that aren't in use.
		}

		private void PollObd(Object src, ElapsedEventArgs e)
		{
			InvokeOnMainThread(() => { 				
				try
				{
					var data = obd.Read();				

					if (data == null)
					{
						textView.Text = "\nerror reading obd data:\n";

						return;
					}					

					if (data["spd"] != "-255")
					{
						var speedInMilesPerHour = (int)(Convert.ToInt32(data["spd"]) * 0.621371);
						var rpm = data["rpm"] == "-255" ? "0" : data["rpm"];

						textView.Text = "Speed: " + speedInMilesPerHour + " mph\nRPM: " + rpm;

                        // TODO: need to connect to GIS service to get the speed limit at the current location
						if (speedInMilesPerHour > 35)
						{
							textView.TextColor = UIColor.Red;
							textView.Text += "\nPlease slow down...";
						}
						else
						{
							textView.TextColor = UIColor.Black;
						}
					}
				}
				catch (Exception ex)
				{
					textView.Text = ex.Message;
				}
			});
		}
	}
}