using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using tckr.Models;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;

namespace tckr.Controllers
{
    public class MainController : Controller
    {
        private tckrContext _context;
        private static List<Dictionary<string, string>> _stocks;
        
        public MainController(tckrContext context)
        {
            _context = context;
            _stocks = new List<Dictionary<string, string>>();

            // Store API values locally
            WebRequest.GetList(JsonResponse =>
                {
                    _stocks = JsonResponse;
                }
            ).Wait();
        }


        [HttpGet]
        [Route("Watchlist")]
        public IActionResult Watchlist()
        {
            // Nav Bar will be checking to see if ViewBag.Id is valid
            var SessionId = HttpContext.Session.GetInt32("LoggedUserId");
            ViewBag.Id = SessionId;
            // Retreive id from Session for User query
            int? id = HttpContext.Session.GetInt32("LoggedUserId");
            if (id == null)
            {
                return RedirectToAction("Index", "User");
            }

            // Retreive current User and Portfolio from the database
            User User = _context.Users.SingleOrDefault(u => u.Id == (int)id);
            Watchlist Watchlist = _context.Watchlists
                .Include(p => p.Stocks)
                .SingleOrDefault(p => p.User == User);
            
            // For each Stock in Portfolio, call API based on values in database
            // Also, populate Stocks list for later use in ViewBag
            ViewBag.Total = 0;
            foreach (Stock Stock in Watchlist.Stocks)
            {
                // Create a Dictionary object to store JSON values from API call
                Dictionary<string, object> Data = new Dictionary<string, object>();
                
                // Make API call
                WebRequest.GetQuote(Stock.Symbol, JsonResponse =>
                    {
                        Data = JsonResponse;
                    }
                ).Wait();

                // We can save each Dictionary, containing each stock, to a List, then pass that List to the View using ViewBag. 
                //This will allow more flexibility when making changes and will reduce the number of collumns in our DB table.

                
                // Define values for each stock to be stored in ViewBag
                double CurrentPrice = Convert.ToDouble(Data["latestPrice"]);
                
                Stock.Name = (string)Data["companyName"];
                Stock.CurrentPrice = CurrentPrice;
                Stock.Week52Low = Convert.ToDouble(Data["week52Low"]);
                Stock.Week52High = Convert.ToDouble(Data["week52High"]);
                Stock.UpdatedAt = DateTime.Now;

                _context.SaveChanges();

                ViewBag.Total += Stock.CurrentValue;
            }
            
            // Store values in ViewBag for Portfolio page rendering
            ViewBag.Watchlist = Watchlist;
            ViewBag.User = User;
            return View("watchlist");
        }


        // Add a new stock to your Watchlist
        [HttpPost]
        [Route("WatchlistAdd")]
        public IActionResult WatchlistAdd(AllStockViewModels s)
        {
            int? id = HttpContext.Session.GetInt32("LoggedUserId");

            if (id == null)
            {
                return RedirectToAction("Index", "User");
            }

            // Get the User object based on the id stored in session.
            User User = _context.Users.SingleOrDefault(u => u.Id == (int)id);
            // Get the Watchlist and Stocks of the user.
            Watchlist Watchlist = _context.Watchlists
                .Include(w => w.Stocks)
                .SingleOrDefault(p => p.User == User);
            
            if (ModelState.IsValid)
            {
                try
                {
                    Dictionary<string, object> Data = new Dictionary<string, object>();
                    // Make a API call to ensure that the inputted ticker/symbol is a valid one before storing it in user's list of Stocks.
                    WebRequest.GetQuote(s.WatchlistStockViewModel.Symbol, JsonResponse =>
                    {
                        Data = JsonResponse;
                    }
                    ).Wait();


                    Stock NewStock = new Stock
                    {
                        Symbol = s.WatchlistStockViewModel.Symbol,
                        Name = (string)Data["companyName"],
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                    };

                    Watchlist.Stocks.Add(NewStock);
                    _context.Add(NewStock);
                    _context.SaveChanges();
                    
                    return RedirectToAction("Watchlist");
                }
                // Catch will run if the ticker/symbol submitted was not found in the DB.
                catch
                {
                    // Return reason for error
                    TempData["NewStockError"] = "That stock does not exist in our database. Please try again.";
                    return RedirectToAction("Watchlist");
                }
            }

            // If ModelState is invalid, return View with validation errors and also make API call to retrieve proper data to display (Stock data, place User in viewbag, place error in ViewBag)
            // For each Stock in Portfolio, call API based on values in database
            // Also, populate Stocks list for later use in ViewBag
            ViewBag.Total = 0;
            foreach (Stock Stock in Watchlist.Stocks)
            {
                // Create a Dictionary object to store JSON values from API call
                Dictionary<string, object> Data = new Dictionary<string, object>();

                // Make API call
                WebRequest.GetQuote(Stock.Symbol, JsonResponse =>
                    {
                        Data = JsonResponse;
                    }
                ).Wait();

                // Define values for each stock to be stored in ViewBag
                double CurrentPrice = Convert.ToDouble(Data["latestPrice"]);

                Stock.Name = (string)Data["companyName"];
                Stock.PurchaseValue = Stock.PurchasePrice * Stock.Shares;
                Stock.CurrentPrice = CurrentPrice;
                Stock.CurrentValue = CurrentPrice * Stock.Shares;
                Stock.GainLossPrice = CurrentPrice - Stock.PurchasePrice;
                Stock.GainLossValue = (CurrentPrice - Stock.PurchasePrice) * Stock.Shares;
                Stock.GainLossPercent = 100 * (CurrentPrice - Stock.PurchasePrice) / (Stock.PurchasePrice);
                Stock.Week52Low = Convert.ToDouble(Data["week52Low"]);
                Stock.Week52High = Convert.ToDouble(Data["week52High"]);
                Stock.UpdatedAt = DateTime.Now;

                _context.SaveChanges();

                ViewBag.Total += Stock.CurrentValue;
            }

            // Store values in ViewBag for Portfolio page rendering
            ViewBag.Watchlist = Watchlist;
            ViewBag.User = User;
            
            return View("Watchlist");
        }



        [HttpGet]
        [Route("Portfolio")]
        public IActionResult Portfolio()
        {
            // Nav Bar will be checking to see if ViewBag.Id is valid
            var SessionId = HttpContext.Session.GetInt32("LoggedUserId");
            ViewBag.Id = SessionId;
            // Retreive id from Session for User query
            int? id = HttpContext.Session.GetInt32("LoggedUserId");
            
            if (id == null)
            {
                return RedirectToAction("Index", "User");
            }

            // Retreive current User and Portfolio from the database
            User User = _context.Users.SingleOrDefault(u => u.Id == (int)id);
            Portfolio Portfolio = _context.Portfolios
                .Include(p => p.Stocks)
                .SingleOrDefault(p => p.User == User);
            
            // Instatiate ViewBags for Total Value and Total Gain/Loss value. 
            ViewBag.Total = 0;
            ViewBag.TotalGainLossValue = 0; 
            // For each Stock in Portfolio, call API based on values in database
            // Also, populate Stocks list for later use in ViewBag
            foreach (Stock Stock in Portfolio.Stocks)
            {
                // Create a Dictionary object to store JSON values from API call
                Dictionary<string, object> Data = new Dictionary<string, object>();
                
                // Make API call
                WebRequest.GetQuote(Stock.Symbol, JsonResponse =>
                    {
                        Data = JsonResponse;
                    }
                ).Wait();

                
                // Define values for each stock to be stored in ViewBag
                double CurrentPrice = Convert.ToDouble(Data["latestPrice"]);

                
                
                Stock.Name = (string)Data["companyName"];
                Stock.PurchaseValue = Stock.PurchasePrice * Stock.Shares;
                Stock.CurrentPrice = CurrentPrice;
                Stock.CurrentValue = CurrentPrice * Stock.Shares;
                Stock.GainLossPrice = CurrentPrice - Stock.PurchasePrice;
                Stock.GainLossValue = (CurrentPrice - Stock.PurchasePrice) * Stock.Shares;
                Stock.GainLossPercent = 100 * (CurrentPrice - Stock.PurchasePrice) / (Stock.PurchasePrice);
                Stock.Week52Low = Convert.ToDouble(Data["week52Low"]);
                Stock.Week52High = Convert.ToDouble(Data["week52High"]);
                Stock.UpdatedAt = DateTime.Now;

                _context.SaveChanges();

                ViewBag.Total += Stock.CurrentValue;
                
                ViewBag.TotalGainLossValue += Stock.GainLossValue;
            }
            
            // Store values in ViewBag for Portfolio page rendering
            ViewBag.Portfolio = Portfolio;
            ViewBag.User = User;
            return View("portfolio");
        }

        [HttpPost]
        [Route("PortfolioAdd")]
        public IActionResult PortfolioAdd(AllStockViewModels s)
        {
            int? id = HttpContext.Session.GetInt32("LoggedUserId");

            if (id == null)
            {
                return RedirectToAction("Index", "User");
            }

            User User = _context.Users.SingleOrDefault(u => u.Id == (int)id);
            Portfolio Portfolio = _context.Portfolios
                .Include(p => p.Stocks)
                .SingleOrDefault(p => p.User == User);
            
            if (ModelState.IsValid)
            {
                try
                {
                    Dictionary<string, object> Data = new Dictionary<string, object>();
                    // Make a API call to ensure that the inputted ticker/symbol is a valid one before storing it in user's list of Stocks.
                    WebRequest.GetQuote(s.PortfolioStockViewModel.Symbol, JsonResponse =>
                    {
                        Data = JsonResponse;
                    }
                    ).Wait();


                    Stock NewStock = new Stock
                    {
                        Symbol = s.PortfolioStockViewModel.Symbol,
                        Shares = s.PortfolioStockViewModel.Shares,
                        Name = (string)Data["companyName"],
                        PurchasePrice = s.PortfolioStockViewModel.PurchasePrice,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                    };

                    Portfolio.Stocks.Add(NewStock);
                    _context.Add(NewStock);
                    _context.SaveChanges();
                    
                    return RedirectToAction("Portfolio");
                }
                // Catch will run if the ticker/symbol submitted was not found in the DB.
                catch
                {
                    // Return reason for error
                    TempData["NewStockError"] = "That stock does not exist in our database. Please try again.";
                    // Return vital data for view.
                    // ViewBag.Portfolio = Portfolio;
                    // ViewBag.User = User;
                    return RedirectToAction("Portfolio");
                }
            }

            // For each Stock in Portfolio, call API based on values in database
            // Also, populate Stocks list for later use in ViewBag
            ViewBag.Total = 0;
            ViewBag.TotalGainLossValue = 0;
            foreach (Stock Stock in Portfolio.Stocks)
            {
                // Create a Dictionary object to store JSON values from API call
                Dictionary<string, object> Data = new Dictionary<string, object>();

                // Make API call
                WebRequest.GetQuote(Stock.Symbol, JsonResponse =>
                    {
                        Data = JsonResponse;
                    }
                ).Wait();

                // Define values for each stock to be stored in ViewBag
                double CurrentPrice = Convert.ToDouble(Data["latestPrice"]);

                Stock.Name = (string)Data["companyName"];
                Stock.PurchaseValue = Stock.PurchasePrice * Stock.Shares;
                Stock.CurrentPrice = CurrentPrice;
                Stock.CurrentValue = CurrentPrice * Stock.Shares;
                Stock.GainLossPrice = CurrentPrice - Stock.PurchasePrice;
                Stock.GainLossValue = (CurrentPrice - Stock.PurchasePrice) * Stock.Shares;
                Stock.GainLossPercent = 100 * (CurrentPrice - Stock.PurchasePrice) / (Stock.PurchasePrice);
                Stock.Week52Low = Convert.ToDouble(Data["week52Low"]);
                Stock.Week52High = Convert.ToDouble(Data["week52High"]);
                Stock.UpdatedAt = DateTime.Now;

                _context.SaveChanges();

                ViewBag.Total += Stock.CurrentValue;
                
                ViewBag.TotalGainLossValue += Stock.GainLossValue;
            }

            // Store values in ViewBag for Portfolio page rendering
            ViewBag.Portfolio = Portfolio;
            ViewBag.User = User;
            
            return View("portfolio");
        }

        [HttpGet]
        [Route("Search/Name/{Name}")]
        public List<Dictionary<string, string>> SearchName(string Name)
        {
            List<Dictionary<string, string>> Matches = new List<Dictionary<string, string>>();
            
            int limit = 10;
            foreach (var Stock in _stocks)
            {
                if (limit == 0)
                {
                    break;
                }
                foreach (KeyValuePair<string, string> Pair in Stock)
                {
                    if (Pair.Key == "name" && Pair.Value.Contains(Name))
                    {
                        Matches.Add(new Dictionary<string, string>(Stock));
                        limit--;
                    }
                }
            }

            return Matches;
        }

        [HttpGet]
        [Route("Search/Symbol/{Symbol}")]
        public List<Dictionary<string, string>> SearchSymbol(string Symbol)
        {
            List<Dictionary<string, string>> Matches = new List<Dictionary<string, string>>();
            
            int limit = 10;
            foreach (var Stock in _stocks)
            {
                if (limit == 0)
                {
                    break;
                }
                foreach (KeyValuePair<string, string> Pair in Stock)
                {
                    if (Pair.Key == "symbol" && Pair.Value.Contains(Symbol))
                    {
                        Matches.Add(new Dictionary<string, string>(Stock));
                        limit--;
                    }
                }
            }

            return Matches;
        }

        [HttpGet]
        [Route("Chart")]
        public IActionResult Chart(string Symbol)
        {
            int? id = HttpContext.Session.GetInt32("LoggedUserId");

            if (id == null)
            {
                return RedirectToAction("Index", "User");
            }

            ViewBag.Id = (int)id;
            
            return View("stock");
        }
    }
}