set BASE_NAME=epub_data
set RELEASE_NAME=â∆Ç¬Ç≠ÇËÉXÉLÉãÇ≈àŸê¢äE 01

cd /D "%~dp0\%BASE_NAME%"
rm "%~dp0%BASE_NAME%.epub"
rm "%~dp0%RELEASE_NAME%.epub"
copy "F:\Storage\book\epub\Template\style\*.css" "%~dp0%BASE_NAME%\item\style"

"%~dp0zip" -0 -X "%~dp0%BASE_NAME%.epub" mimetype
"%~dp0zip" -r "%~dp0%BASE_NAME%.epub" * -x mimetype

cd ../
ren "%BASE_NAME%.epub" "%RELEASE_NAME%.epub"

