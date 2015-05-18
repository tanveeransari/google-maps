require(XML);require(stringr);
library(XML)
library(dplyr)
library(stringr)
rootURL<-"http://www.motorcycleroads.com/"

pg<-htmlParse("http://www.motorcycleroads.com/")
links<-getNodeSet(pg,"//*[@id=\"copy\"]/p[3]/a")
linkshref<-getNodeSet(pg,"//*[@id=\"copy\"]/p[3]/a/@href")
hrfExtr<-sapply(linkshref,function(x){str_extract(x,"[0-9]+$")})
rm(linkshref)
rm(pg)

#find hyperlinks for each state page- each state has an id number for it
urls=paste("http://www.motorcycleroads.com/best/?c=",hrfExtr,sep = "")
rm(hrfExtr)

for(i in 1:length(urls)) {
  #For each state go to best routes page
  l<-urls[i]
  pg<-htmlParse(l)

  # On routes page travel each hyperlink to find all routes pages
  allPgLinks<-getNodeSet(pg,"//*/a/@href")
  routeLinks<-allPgLinks[grep("^[0-9]+",allPgLinks)]
  for(j in 1:length(routeLinks))
  {
<<<<<<< HEAD
    routeLink<-paste(rootURL,as.character(routeLinks[j]),sep="")
=======
    routeLink<-paste(rootURL,as.character(routeLinks[1]),sep="")
>>>>>>> origin/master

    pg<-htmlParse(routeLink)
    routePgLinks<-getNodeSet(pg,"//*/a/@href")
    # On individual route page find hyperlink to gpx files
    if(length(routePgLinks[grep("gpx",routePgLinks)])>0) {

      gpxUrl<-sub("./",rootURL, as.character(routePgLinks[grep("gpx",routePgLinks)][1]), fixed=T)
      gpxFileName<-str_split(gpxUrl,"=")[[1]][2]
      # download gpx file if not exists
      if(!file.exists(gpxFileName)) {
        download.file(gpxUrl,gpxFileName)
      }
    }
  }
}


# if(!file.exists(fileName)) {
#   download.file(fileNameHyperlink, fileName)
# }


# stateNames<-c("Alabama","Alaska",  "Arizona",	"Arkansas",	"California",	"Colorado",	"Connecticut",
#               "Delaware",	"Florida",	"Georgia",	"Hawaii",	"Idaho",	"Illinois",	"Indiana",
#               "Iowa",	"Kansas",	"Kentucky",	"Louisiana",	"Maine",	"Maryland",	"Massachusetts",
#               "Michigan",	"Minnesota",	"Mississippi",	"Missouri",	"Montana",	"Nebraska",	"Nevada",
#               "New Hampshire",	"New Jersey",	"New Mexico",	"New York",	"North Carolina",
#               "North Dakota",	"Ohio",	"Oklahoma",	"Oregon",	"Pennsylvania",	"Rhode Island",
#               "South Carolina",	"South Dakota",	"Tennessee",	"Texas",	"Utah",	"Vermont",
#               "Virginia",	"Washington",	"West Virginia",	"Wisconsin",	"Wyoming")
