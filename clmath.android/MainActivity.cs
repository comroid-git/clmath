using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Views;
using Android.Widget;

namespace clmath.android
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        public EditText FuncInput { get; private set; }
        public Button BtnEval { get; private set; }
        public TextView ResultOutput { get; private set; }
        public LinearLayout VarsList { get; private set; }
        public MathContext Ctx { get; } = new MathContext();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            
            FuncInput = FindViewById<EditText>(Resource.Id.funcInput)!;
            BtnEval = FindViewById<Button>(Resource.Id.btnEval)!;
            ResultOutput = FindViewById<TextView>(Resource.Id.resultOutput);
            VarsList = FindViewById<LinearLayout>(Resource.Id.varsList);
            
            BtnEval.Click += evalFunc;
        }

        public void evalFunc(object sender, EventArgs args)
        {
            var func = Program.ParseFunc(FuncInput.Text!);

            if (func.EnumerateVars().Any(var => !Ctx.var.ContainsKey(var)))
            {
                Snackbar.Make(FuncInput, "Please set all variables and try again", 3000).Show();
                RefreshVarsList(func.EnumerateVars());
            } else ResultOutput.Text = func.Evaluate(Ctx).ToString();
        }

        private void RefreshVarsList(List<string> vars)
        {
            VarsList.RemoveViews(0, VarsList.ChildCount);
            foreach (var var in vars)
                VarsList.AddView(PrepareVarBox(var));
        }

        private LinearLayout PrepareVarBox(string var)
        {
            var box = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            EditText input;
            box.AddView(new TextView(this) { Text = var, Left = 10 });
            box.AddView(input = new EditText(this) { Hint = "Value for " + var, Right = 10 });
            input.AfterTextChanged += (sender, _) => ChangeVar(var, (sender as EditText)!.Text);
            return box;
        }

        private void ChangeVar(string var, string text)
        {
            var value = Program.ConvertValueFromString($"{var} = {text}")!.Value.value;
            Ctx.var[var] = value;
        }
    }
}