using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.Widget;
using Android.Views;
using Android.Widget;
using V7Toolbar = Android.Support.V7.Widget.Toolbar;

namespace clmath.android
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        public DrawerLayout drawerLayout { get; private set; }
        public EditText FuncInput { get; private set; }
        public Button BtnEval { get; private set; }
        public TextView ResultOutput { get; private set; }
        public LinearLayout VarsList { get; private set; }
        public MathContext Ctx { get; } = new MathContext();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            // Create UI
            SetContentView(Resource.Layout.activity_main);
            drawerLayout = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);

            // Init toolbar
            var toolbar = FindViewById<V7Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);	

            // Attach item selected handler to navigation view
            var navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            navigationView.NavigationItemSelected += NavigationView_NavigationItemSelected;

            // Create ActionBarDrawerToggle button and add it to the toolbar
            var drawerToggle = new ActionBarDrawerToggle(this, drawerLayout, toolbar, Resource.String.open_drawer, Resource.String.close_drawer);
            drawerLayout.SetDrawerListener(drawerToggle);
            drawerToggle.SyncState();

            FuncInput = FindViewById<EditText>(Resource.Id.funcInput)!;
            BtnEval = FindViewById<Button>(Resource.Id.btnEval)!;
            ResultOutput = FindViewById<TextView>(Resource.Id.resultOutput);
            VarsList = FindViewById<LinearLayout>(Resource.Id.varsList);

            BtnEval.Click += evalFunc;
        }

        private void NavigationView_NavigationItemSelected(object sender, NavigationView.NavigationItemSelectedEventArgs e)
        {
            switch (e.MenuItem.ItemId)
            {
                case Resource.Id.nav_calc: break;
                case Resource.Id.nav_funcs: break;
                case Resource.Id.nav_constants: break;
                case Resource.Id.nav_units: break;
                case Resource.Id.nav_graph: break;
            }

            // Close drawer
            drawerLayout.CloseDrawers();
        }

        public void evalFunc(object sender, EventArgs args)
        {
            var func = Program.ParseFunc(FuncInput.Text!);

            var vars = func.EnumerateVars().Where(var => !Program.constants.ContainsKey(var)).ToList();
            if (vars.Any(var => !Ctx.var.ContainsKey(var)))
            {
                Snackbar.Make(FuncInput, "Please set all variables and try again", 3000).Show();
                RefreshVarsList(vars);
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
            box.AddView(input = new EditText(this)
            {
                Text = Ctx.var.ContainsKey(var) ? Ctx.var[var].ToString() : string.Empty,
                Hint = "Value for " + var,
                Left = 100
            });
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