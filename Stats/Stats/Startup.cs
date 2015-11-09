using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Stats.Startup))]
namespace Stats
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
