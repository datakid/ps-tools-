:: @id        user.inspect.listening-ports
:: @title     Listening ports
:: @menu      Inspect
:: @when      folder, background
:: @output    console
:: @risk      safe
:: @keywords  netstat, port, server
:: @description Shows which programs are listening on a port.
netstat -ano | findstr LISTENING
